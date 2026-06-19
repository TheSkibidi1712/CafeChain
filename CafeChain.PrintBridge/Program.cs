using CafeChain.PrintBridge;
using CafeChain.PrintBridge.Services;

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
