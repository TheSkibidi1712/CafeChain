using System.Diagnostics;
using CafeChain.Application.Interfaces.Admin.Staffs;
using CafeChain.Application.Options;
using CafeChain.Infrastructure.Interfaces.Admin.Staffs;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Workers;

public sealed class StaffScheduleGapNotificationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly StaffScheduleGapNotificationOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StaffScheduleGapNotificationWorker> _logger;

    public StaffScheduleGapNotificationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<StaffScheduleGapNotificationOptions> options,
        TimeProvider timeProvider,
        ILogger<StaffScheduleGapNotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Staff schedule gap notification worker is disabled.");
            return;
        }

        await Task.Delay(
            TimeSpan.FromSeconds(Math.Clamp(_options.InitialDelaySeconds, 0, 3600)),
            stoppingToken);
        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(Math.Clamp(_options.IntervalMinutes, 1, 1440)));

        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var scannedStores = 0;
        var created = 0;
        var updated = 0;
        var resolved = 0;
        var gaps = 0;
        var lookaheadDays = Math.Clamp(_options.LookaheadDays, 1, 14);
        var fromDate = _timeProvider.GetLocalNow().Date.AddDays(1);
        var toDate = fromDate.AddDays(lookaheadDays - 1);

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IShiftOptimizationRepository>();
        var service = scope.ServiceProvider
            .GetRequiredService<IStaffScheduleGapNotificationService>();

        foreach (var storeId in await repository.GetActiveStoreIdsAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await service.ScanStoreAsync(
                    storeId, fromDate, toDate, cancellationToken);
                scannedStores++;
                created += result.AlertsCreated;
                updated += result.AlertsUpdated;
                resolved += result.AlertsResolved;
                gaps += result.MissingRequirementCount;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Staff schedule gap scan failed. StoreId={StoreId} FromDate={FromDate} ToDate={ToDate}",
                    storeId,
                    fromDate,
                    toDate);
            }
        }

        _logger.LogInformation(
            "Staff schedule gap scan completed. Stores={Stores} FromDate={FromDate} ToDate={ToDate} Gaps={Gaps} Created={Created} Updated={Updated} Resolved={Resolved} ElapsedMs={ElapsedMs}",
            scannedStores,
            fromDate,
            toDate,
            gaps,
            created,
            updated,
            resolved,
            stopwatch.ElapsedMilliseconds);
    }
}
