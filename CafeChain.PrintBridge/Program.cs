using CafeChain.PrintBridge;
using CafeChain.PrintBridge.Services;

var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environments.Production;
var bootstrapConfiguration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();
var bridgeStoreId = int.TryParse(bootstrapConfiguration["PrintBridge:StoreId"], out var configuredStoreId)
    && configuredStoreId > 0
        ? configuredStoreId
        : 1;

using var instanceMutex = new Mutex(
    initiallyOwned: true,
    name: $"Local\\CafeChain.PrintBridge.Store.{bridgeStoreId}",
    createdNew: out var isFirstInstance);

if (!isFirstInstance)
{
    Console.Error.WriteLine($"CafeChain.PrintBridge cho Store {bridgeStoreId} đã chạy; bỏ qua instance trùng.");
    return;
}

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((hostContext, services) =>
{
    // Bind config section "PrintBridge" → PrintBridgeOptions
    services.Configure<PrintBridgeOptions>(
        hostContext.Configuration.GetSection("PrintBridge"));

    // DI: Services
    services.AddSingleton<TcpPrinterForwarder>();
    services.AddSingleton<SignalRPrintClient>();

    // DI: Worker
    services.AddHostedService<Worker>();
});

var host = builder.Build();
host.Run();
