using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Infrastructure.Interfaces.Admin.POS;

namespace CafeChain.Application.Workers;

public sealed class OtpExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OtpExpiryWorker> _logger;

    public OtpExpiryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OtpExpiryWorker> logger,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireOnceAsync(stoppingToken);
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OTP_EXPIRY_WORKER_FAILED");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private async Task ExpireOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOtpChallengeRepository>();
        var publisher = scope.ServiceProvider.GetService<IOperationalOtpNotificationPublisher>();
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var expired = await repository.ExpireDueChallengesAsync(nowUtc, cancellationToken);
        if (publisher == null) return;

        foreach (var item in expired.Where(x => x.NotificationId.HasValue))
        {
            await publisher.PublishChangedAsync(
                item.ApproverStaffId,
                new OperationalOtpNotificationChangedDto(
                    Guid.NewGuid().ToString("N"),
                    item.NotificationId!.Value,
                    "Expired",
                    nowUtc),
                cancellationToken);
        }
    }
}
