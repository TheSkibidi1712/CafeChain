using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Options;
using CafeChain.Infrastructure.Interfaces.Analytics;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Workers;

public sealed class PosRecommendationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory; private readonly PosRecommendationOptions _options; private readonly ILogger<PosRecommendationWorker> _logger;
    public PosRecommendationWorker(IServiceScopeFactory scopeFactory, IOptions<PosRecommendationOptions> options, ILogger<PosRecommendationWorker> logger) { _scopeFactory = scopeFactory; _options = options.Value; _logger = logger; }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            var started = DateTime.UtcNow; using var scope = _scopeFactory.CreateScope(); var repo = scope.ServiceProvider.GetRequiredService<IPosRecommendationRepository>(); var service = scope.ServiceProvider.GetRequiredService<IPosRecommendationService>();
            var stores = await repo.GetActiveStoreIdsAsync(stoppingToken); var succeeded = 0;
            foreach (var storeId in stores) try { await service.RebuildStoreAsync(storeId, stoppingToken); succeeded++; } catch (Exception ex) { _logger.LogError(ex, "POS recommendation build failed for StoreId={StoreId}", storeId); }
            _logger.LogInformation("POS recommendation build finished Stores={Stores} Succeeded={Succeeded} ElapsedMs={ElapsedMs}", stores.Count, succeeded, (DateTime.UtcNow - started).TotalMilliseconds);
            await Task.Delay(TimeSpan.FromHours(Math.Max(1, _options.IntervalHours)), stoppingToken);
        }
    }
}

public sealed class AnomalyDetectionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory; private readonly AnomalyDetectionOptions _options; private readonly ILogger<AnomalyDetectionWorker> _logger;
    public AnomalyDetectionWorker(IServiceScopeFactory scopeFactory, IOptions<AnomalyDetectionOptions> options, ILogger<AnomalyDetectionWorker> logger) { _scopeFactory = scopeFactory; _options = options.Value; _logger = logger; }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            var started = DateTime.UtcNow; using var scope = _scopeFactory.CreateScope(); var repo = scope.ServiceProvider.GetRequiredService<IAnomalyDetectionRepository>(); var service = scope.ServiceProvider.GetRequiredService<IAnomalyDetectionService>();
            var stores = await repo.GetActiveStoreIdsAsync(stoppingToken); var succeeded = 0;
            foreach (var storeId in stores) try { await service.AnalyzeStoreAsync(storeId, stoppingToken); succeeded++; } catch (Exception ex) { _logger.LogError(ex, "Anomaly analysis failed for StoreId={StoreId}", storeId); }
            _logger.LogInformation("Anomaly analysis finished Stores={Stores} Succeeded={Succeeded} ElapsedMs={ElapsedMs}", stores.Count, succeeded, (DateTime.UtcNow - started).TotalMilliseconds);
            await Task.Delay(TimeSpan.FromMinutes(Math.Max(5, _options.IntervalMinutes)), stoppingToken);
        }
    }
}
