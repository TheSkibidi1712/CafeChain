using CafeChain.Application.Tools;
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

// Per-machine overrides (connection strings) — never commit appsettings.Local.json.
// Loaded AFTER default config + User Secrets. Do NOT put empty Email:Password here —
// an empty value overrides User Secrets / Email__Password and breaks SMTP.
// Prefer: dotnet user-secrets set "Email:Password" "..." or env Email__Password.
// See appsettings.Local.json.example and docs/testing/email-otp-local-setup.md.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

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