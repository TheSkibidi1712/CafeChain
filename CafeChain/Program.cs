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
    .AddCafeChainWeb()
    .AddCafeChainDatabase(builder.Configuration)
    .AddCafeChainAuthentication(builder.Configuration, builder.Environment)
    .AddCafeChainAuthorization()
    .AddCafeChainThirdPartyServices(builder.Configuration, builder.Environment)
    .AddCafeChainApplicationServices()
    .AddCafeChainRepositories()
    .AddCafeChainWorkers();

var app = builder.Build();

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
