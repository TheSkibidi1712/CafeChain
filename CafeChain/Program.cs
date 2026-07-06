using CafeChain.Extensions;
using CafeChain.Extensions.Hosting;
using CafeChain.Extensions.Pipeline;
using CafeChain.Extensions.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

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
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}