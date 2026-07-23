using System.Diagnostics;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Options;
using CafeChain.Infrastructure.Interfaces.Admin.Procurement;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Workers;

public sealed class InventoryReorderNotificationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InventoryReorderNotificationOptions _options;
    private readonly ILogger<InventoryReorderNotificationWorker> _logger;

    public InventoryReorderNotificationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<InventoryReorderNotificationOptions> options,
        ILogger<InventoryReorderNotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;
        await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(_options.InitialDelaySeconds, 0, 3600)), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Clamp(_options.IntervalMinutes, 1, 1440)));
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var created = 0;
        var updated = 0;
        var resolved = 0;
        var storeCount = 0;
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReorderSuggestionRepository>();
        var service = scope.ServiceProvider.GetRequiredService<IInventoryReorderNotificationService>();
        var stores = await repository.GetActiveStoreIdsAsync();
        foreach (var storeId in stores)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await service.RefreshStoreAsync(
                    storeId,
                    Math.Clamp(_options.AnalysisWindowDays, 1, 365),
                    cancellationToken);
                created += result.Created;
                updated += result.Updated;
                resolved += result.Resolved;
                storeCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Inventory reorder notification failed for StoreId={StoreId}", storeId);
            }
        }
        _logger.LogInformation(
            "Inventory reorder notification completed. Stores={Stores} Created={Created} Updated={Updated} Resolved={Resolved} ElapsedMs={ElapsedMs}",
            storeCount, created, updated, resolved, stopwatch.ElapsedMilliseconds);
    }
}
