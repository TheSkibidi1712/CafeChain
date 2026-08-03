using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Data;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Workers;

public sealed class WorkShiftExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WorkShiftExpiryWorker> _logger;

    public WorkShiftExpiryWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<WorkShiftExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), _timeProvider);
        await ProcessAsync(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ProcessAsync(stoppingToken);
    }

    internal async Task ProcessAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var publisher = scope.ServiceProvider.GetRequiredService<IWorkShiftNotificationPublisher>();
            var audit = scope.ServiceProvider.GetRequiredService<IWorkShiftAuditService>();
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            var ids = await db.WorkShifts.AsNoTracking()
                .Where(x => x.OpenContext == WorkShiftOpenContexts.OutsideSchedule
                    && x.Status == WorkShiftStatuses.Open
                    && x.AutoCloseAtUtc.HasValue
                    && x.AutoCloseAtUtc.Value <= nowUtc.AddMinutes(30))
                .Select(x => x.ShiftId)
                .ToListAsync(cancellationToken);

            foreach (var id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WorkShiftNotificationDto? notification = null;
                await using var transaction = await db.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable,
                    cancellationToken);
                try
                {
                    var shift = db.Database.IsSqlServer()
                        ? await db.WorkShifts
                            .FromSqlInterpolated($@"
SELECT *
FROM [WorkShifts] WITH (UPDLOCK, READPAST, ROWLOCK)
WHERE [ShiftId] = {id} AND [Status] = {WorkShiftStatuses.Open}")
                            .FirstOrDefaultAsync(cancellationToken)
                        : await db.WorkShifts.FirstOrDefaultAsync(
                            x => x.ShiftId == id && x.Status == WorkShiftStatuses.Open,
                            cancellationToken);
                    if (shift == null || !shift.AutoCloseAtUtc.HasValue)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        continue;
                    }

                    var remaining = shift.AutoCloseAtUtc.Value - nowUtc;
                    var warningLevel = remaining <= TimeSpan.Zero ? (byte)4
                        : remaining <= TimeSpan.FromMinutes(1) ? (byte)3
                        : remaining <= TimeSpan.FromMinutes(10) ? (byte)2
                        : (byte)1;
                    if (warningLevel <= shift.ExpiryWarningLevel)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        continue;
                    }

                    var eventType = warningLevel switch
                    {
                        1 => "EXPIRY_WARNING_30",
                        2 => "EXPIRY_WARNING_10",
                        3 => "EXPIRY_WARNING_1",
                        _ => "EXPIRED"
                    };

                    if (warningLevel == 4)
                    {
                        var hasOrders = await db.Orders.AnyAsync(x => x.WorkShiftId == shift.ShiftId, cancellationToken);
                        var isCompletelyEmpty = !hasOrders
                            && shift.StartingCash == 0
                            && !shift.RequiresReconciliation
                            && !shift.HasLateOfflineSync
                            && (shift.OfflineOrderCountAtClose ?? 0) == 0;
                        shift.ExpiredAtUtc = nowUtc;
                        if (isCompletelyEmpty)
                        {
                            shift.ExpectedEndingCash = 0;
                            shift.ActualEndingCash = 0;
                            shift.CashDiscrepancy = 0;
                            shift.EndTimeUtc = nowUtc;
                            shift.CloseType = WorkShiftCloseTypes.AutoEmptyShift;
                            shift.Status = WorkShiftStatuses.Closed;
                            eventType = "AUTO_EMPTY_CLOSED";
                        }
                        else
                        {
                            shift.Status = WorkShiftStatuses.ExpiredPendingClose;
                        }
                    }

                    shift.ExpiryWarningLevel = warningLevel;
                    await audit.WriteAsync(
                        eventType,
                        shift.ShiftId,
                        0,
                        new { Status = WorkShiftStatuses.Open },
                        new { shift.Status, shift.ExpiredAtUtc, shift.CloseType },
                        cancellationToken);
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    notification = new WorkShiftNotificationDto
                    {
                        WorkShiftId = shift.ShiftId,
                        StoreId = shift.StoreId,
                        StaffId = shift.UserId,
                        TerminalId = shift.PosTerminalId,
                        EventType = eventType,
                        Status = shift.Status,
                        ServerNowUtc = nowUtc,
                        AutoCloseAtUtc = shift.AutoCloseAtUtc,
                        RemainingMinutes = Math.Max(0, (int)Math.Ceiling(remaining.TotalMinutes))
                    };
                    _logger.LogInformation(
                        "WORKSHIFT_EXPIRY_EVENT | ShiftId={ShiftId} Event={Event} Status={Status}",
                        shift.ShiftId, eventType, shift.Status);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogWarning(ex, "WORKSHIFT_EXPIRY_CONCURRENCY | ShiftId={ShiftId}", id);
                }

                if (notification != null)
                    await publisher.PublishAsync(notification, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WORKSHIFT_EXPIRY_WORKER_FAILED");
        }
    }
}
