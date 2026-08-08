using CafeChain.Application.Interfaces.POS;

namespace CafeChain.Application.Workers;

public sealed class WorkShiftOpenApprovalExpiryWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkShiftOpenApprovalExpiryWorker> _logger;

    public WorkShiftOpenApprovalExpiryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<WorkShiftOpenApprovalExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IWorkShiftOpenApprovalService>();
                await service.ExpireDueAsync(stoppingToken);
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WORKSHIFT_LATE_OPEN_APPROVAL_EXPIRY_FAILED");
                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
