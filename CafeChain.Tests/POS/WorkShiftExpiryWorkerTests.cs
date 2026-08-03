using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Services.POS;
using CafeChain.Application.Workers;
using CafeChain.Data;
using CafeChain.Models.Stores;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeChain.Tests.POS;

public sealed class WorkShiftExpiryWorkerTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 5, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExpiredEmptyOutsideShift_IsClosedWithoutInventingCash()
    {
        await SeedShiftAsync(startingCash: 0, autoCloseAtUtc: Now.UtcDateTime.AddMinutes(-1));
        var (worker, publisher, provider) = CreateWorker();
        using (provider)
        {
            await worker.ProcessAsync(CancellationToken.None);
        }

        await using var db = CreateDbContext();
        var shift = await db.WorkShifts.FindAsync(1);
        Assert.NotNull(shift);
        Assert.Equal(WorkShiftStatuses.Closed, shift.Status);
        Assert.Equal(WorkShiftCloseTypes.AutoEmptyShift, shift.CloseType);
        Assert.Equal(0, shift.ActualEndingCash);
        Assert.Single(publisher.Notifications, x => x.EventType == "AUTO_EMPTY_CLOSED");
    }

    [Fact]
    public async Task ExpiredShiftWithStartingCash_WaitsForHumanCashCount()
    {
        await SeedShiftAsync(startingCash: 100_000, autoCloseAtUtc: Now.UtcDateTime.AddSeconds(-1));
        var (worker, publisher, provider) = CreateWorker();
        using (provider)
        {
            await worker.ProcessAsync(CancellationToken.None);
        }

        await using var db = CreateDbContext();
        var shift = await db.WorkShifts.FindAsync(1);
        Assert.NotNull(shift);
        Assert.Equal(WorkShiftStatuses.ExpiredPendingClose, shift.Status);
        Assert.Null(shift.ActualEndingCash);
        Assert.Null(shift.EndTimeUtc);
        Assert.Single(publisher.Notifications, x => x.EventType == "EXPIRED");
    }

    [Fact]
    public async Task WarningThirtyMinutes_IsPublishedAndAuditedOnce()
    {
        await SeedShiftAsync(startingCash: 0, autoCloseAtUtc: Now.UtcDateTime.AddMinutes(29));
        var (worker, publisher, provider) = CreateWorker();
        using (provider)
        {
            await worker.ProcessAsync(CancellationToken.None);
            await worker.ProcessAsync(CancellationToken.None);
        }

        await using var db = CreateDbContext();
        var shift = await db.WorkShifts.FindAsync(1);
        Assert.NotNull(shift);
        Assert.Equal((byte)1, shift.ExpiryWarningLevel);
        Assert.Single(publisher.Notifications, x => x.EventType == "EXPIRY_WARNING_30");
        Assert.Equal(1, db.AuditLogs.Count(x => x.Action == "EXPIRY_WARNING_30"));
    }

    private async Task SeedShiftAsync(decimal startingCash, DateTime autoCloseAtUtc)
    {
        await using var db = CreateDbContext();
        db.WorkShifts.Add(new WorkShift
        {
            ShiftId = 1,
            StoreId = 1,
            UserId = 1,
            StartTimeUtc = Now.UtcDateTime.AddHours(-6),
            BusinessDate = new DateTime(2026, 8, 3),
            OpenContext = WorkShiftOpenContexts.OutsideSchedule,
            OutsideScheduleReason = "Hỗ trợ cửa hàng theo điều phối khẩn cấp.",
            AutoCloseAtUtc = autoCloseAtUtc,
            StartingCash = startingCash,
            ExpectedEndingCash = startingCash,
            Status = WorkShiftStatuses.Open
        });
        await db.SaveChangesAsync();
    }

    private (WorkShiftExpiryWorker Worker, CapturingPublisher Publisher, ServiceProvider Provider) CreateWorker()
    {
        var publisher = new CapturingPublisher();
        var services = new ServiceCollection();
        services.AddScoped<AppDbContext>(_ => new TestDbContext(DbOptions));
        services.AddScoped<IWorkShiftAuditService, WorkShiftAuditService>();
        services.AddSingleton<IWorkShiftNotificationPublisher>(publisher);
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        var provider = services.BuildServiceProvider();
        var worker = new WorkShiftExpiryWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<TimeProvider>(),
            NullLogger<WorkShiftExpiryWorker>.Instance);
        return (worker, publisher, provider);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class CapturingPublisher : IWorkShiftNotificationPublisher
    {
        public List<WorkShiftNotificationDto> Notifications { get; } = [];

        public Task PublishAsync(
            WorkShiftNotificationDto notification,
            CancellationToken cancellationToken = default)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
    }
}
