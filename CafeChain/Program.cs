using CafeChain.Application.Tools;
using CafeChain.Application.Options;
using CafeChain.Extensions;
using CafeChain.Extensions.Hosting;
using CafeChain.Extensions.Pipeline;
using CafeChain.Extensions.Services;
using Serilog;

// Dev-only read-only audit (Issue #113 Checkpoint A):
//   dotnet run --project CafeChain -- audit-purchase-units [--out path.json]
if (args.Length > 0
    && string.Equals(args[0], "audit-purchase-units", StringComparison.OrdinalIgnoreCase))
{
    return await PurchaseUnitAuditCli.RunAsync(args);
}
if (args.Length > 0
    && string.Equals(args[0], "audit-purchase-orders", StringComparison.OrdinalIgnoreCase))
{
    return await PurchaseOrderConsistencyCli.RunAsync(args);
}

var builder = WebApplication.CreateBuilder(args);

// Default CreateBuilder provider order (see Microsoft.Extensions.Hosting):
//   1) appsettings.json
//   2) appsettings.{Environment}.json
//   3) User Secrets (Development only)
//   4) Environment variables
//   5) Command-line args
//
// Local machine overrides (connection string only). Loaded next, then we re-add
// User Secrets + Environment so empty keys in Local can never wipe Email:Password.
// Prefer: .\scripts\setup-team-otp-email.ps1  OR  env Email__Password.
// See docs/testing/email-otp-local-setup.md
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
}

// Environment variables always win over JSON/Local for Email__Password etc.
builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<POSPaymentOptions>(builder.Configuration.GetSection("POSPayment"));

builder.AddCafeChainSerilog();

builder.Services
    .AddCafeChainWeb(builder.Configuration)
    .AddCafeChainDatabase(builder.Configuration)
    .AddCafeChainDataProtection(builder.Configuration, builder.Environment)
    .AddCafeChainAuthentication(builder.Configuration, builder.Environment)
    .AddCafeChainAuthorization()
    .AddCafeChainThirdPartyServices(builder.Configuration, builder.Environment)
    .AddCafeChainApplicationServices()
    .AddCafeChainRepositories()
    .AddCafeChainWorkers();

var app = builder.Build();

var dataProtectionHosting = app.Services.GetRequiredService<DataProtectionHostingState>();
Log.Information(
    "CafeChain host starting. EnvironmentName={EnvironmentName} Machine={MachineName} ProcessId={ProcessId} " +
    "PersistentDataProtectionKeys={PersistentDataProtectionKeys} KeyDirectoryReady={KeyDirectoryReady} " +
    "DataProtectionRepository={DataProtectionRepository} KeyDirectory={KeyDirectory} SessionStore={SessionStore}",
    builder.Environment.EnvironmentName,
    Environment.MachineName,
    Environment.ProcessId,
    dataProtectionHosting.UsesPersistentKeys,
    dataProtectionHosting.KeyDirectoryReady,
    dataProtectionHosting.RepositoryMode,
    dataProtectionHosting.KeyDirectoryPath,
    "SqlServer");

app.UseCafeChainPipeline();
app.MapCafeChainEndpoints();

try
{
    Log.Information("🚀 CafeChain POS starting up...");
    app.Run();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ Application terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
// Trigger dotnet watch reload for views update - VerifyOtp Master UI 3.0

