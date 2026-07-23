using System.Diagnostics;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Options;
using CafeChain.Infrastructure.Interfaces.Analytics;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Workers;

public sealed class ForecastGenerationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ForecastingOptions _options;
    private readonly ILogger<ForecastGenerationWorker> _logger;
    public ForecastGenerationWorker(IServiceScopeFactory scopeFactory, IOptions<ForecastingOptions> options, ILogger<ForecastGenerationWorker> logger)
    { _scopeFactory = scopeFactory; _options = options.Value; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.RevenueEnabled && !_options.ProductEnabled) return;
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(Math.Clamp(_options.WorkerIntervalHours, 1, 168)));
        do { await RunOnceAsync(stoppingToken); } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var watch = Stopwatch.StartNew(); var completed = 0; var failed = 0;
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IForecastService>();
        var repository = scope.ServiceProvider.GetRequiredService<IForecastRepository>();
        foreach (var storeId in await repository.GetActiveStoreIdsAsync(ct))
        {
            try
            {
                foreach (var horizon in _options.Horizons.Where(x => x is 7 or 30).Distinct())
                {
                    if (_options.RevenueEnabled) { await service.GenerateRevenueAsync(storeId, horizon, ct); completed++; }
                    if (_options.ProductEnabled)
                    {
                        var from = DateTime.UtcNow.Date.AddDays(-Math.Clamp(_options.AnalysisWindowDays, 84, 730));
                        foreach (var drinkId in await repository.GetProductIdsAsync(storeId, from, DateTime.UtcNow.Date, ct))
                        { await service.GenerateProductAsync(storeId, drinkId, horizon, ct); completed++; }
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { failed++; _logger.LogError(ex, "Forecast generation failed for StoreId={StoreId}", storeId); }
        }
        _logger.LogInformation("Forecast worker completed. Runs={Runs} FailedStores={FailedStores} ElapsedMs={ElapsedMs}", completed, failed, watch.ElapsedMilliseconds);
    }
}
