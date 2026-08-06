using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Options;
using CafeChain.Application.Services.POS;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CafeChain.Tests.POS;

public sealed class WorkShiftOpenAssessmentTests
{
    [Fact]
    public async Task WithinSchedule_UsesAbsoluteLocalInterval()
    {
        var schedule = Schedule(new DateTime(2026, 8, 3), 8, 16);
        var result = await CreateService(LocalUtc(2026, 8, 3, 8, 5), schedule)
            .AssessOpenShiftAsync(7, 1, new OpenShiftAssessmentRequestDto { PosTerminalId = "POS-1" });

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("WITHIN_SCHEDULE", result.Data!.OpenContext);
        Assert.False(result.Data.ReasonRequired);
        Assert.False(result.Data.ApprovalRequired);
        Assert.Equal(schedule.StaffShiftId, result.Data.SourceStaffShiftId);
    }

    [Fact]
    public async Task LateMoreThanThirtyMinutes_RequiresReasonAndApproval()
    {
        var result = await CreateService(
                LocalUtc(2026, 8, 3, 8, 31),
                Schedule(new DateTime(2026, 8, 3), 8, 16))
            .AssessOpenShiftAsync(7, 1, new OpenShiftAssessmentRequestDto { PosTerminalId = "POS-1" });

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("LATE_FOR_SCHEDULE", result.Data!.OpenContext);
        Assert.True(result.Data.ReasonRequired);
        Assert.True(result.Data.ApprovalRequired);
        Assert.Equal(31, result.Data.MinutesLate);
    }

    [Fact]
    public async Task NoCandidateSchedule_IsOutsideAndGetsSixHourDeadline()
    {
        var now = LocalUtc(2026, 8, 3, 23, 30);
        var result = await CreateService(now, null)
            .AssessOpenShiftAsync(7, 1, new OpenShiftAssessmentRequestDto { PosTerminalId = "POS-1" });

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("OUTSIDE_SCHEDULE", result.Data!.OpenContext);
        Assert.True(result.Data.ReasonRequired);
        Assert.True(result.Data.ApprovalRequired);
        Assert.Equal(now.UtcDateTime.AddHours(6), result.Data.AutoCloseAtUtc);
    }

    [Theory]
    [InlineData(WorkShiftStatuses.Open, WorkShiftErrorCodes.StaffAlreadyHasOpenShift)]
    [InlineData(WorkShiftStatuses.Closing, WorkShiftErrorCodes.WorkShiftPendingClose)]
    [InlineData(WorkShiftStatuses.ExpiredPendingClose, WorkShiftErrorCodes.WorkShiftPendingClose)]
    public async Task Staff_active_responsibility_blocks_with_status_specific_code(
        string status,
        string expectedCode)
    {
        var active = ActiveShift(status, staffId: 7, terminalId: "POS-OLD");
        var result = await CreateService(LocalUtc(2026, 8, 3, 8, 5), null, active)
            .AssessOpenContextAsync(7, 1, "POS-NEW");

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal(active.ShiftId, result.Data!.BlockingWorkShift!.WorkShiftId);
        Assert.True(result.Data.BlockingWorkShift.IsOwnedByRequester);
        Assert.Equal(status == WorkShiftStatuses.Open
                ? WorkShiftRecommendedActions.ResumeExistingWorkShift
                : status == WorkShiftStatuses.Closing
                    ? WorkShiftRecommendedActions.CompleteClosing
                    : WorkShiftRecommendedActions.CountAndClose,
            result.Data.RecommendedAction);
        Assert.DoesNotContain("WORKSHIFT_EXPIRED", result.ErrorCode ?? string.Empty, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(WorkShiftStatuses.Open)]
    [InlineData(WorkShiftStatuses.Closing)]
    [InlineData(WorkShiftStatuses.ExpiredPendingClose)]
    public async Task Terminal_active_responsibility_always_returns_terminal_conflict(string status)
    {
        var active = ActiveShift(status, staffId: 8, terminalId: "POS-1");
        var result = await CreateService(LocalUtc(2026, 8, 3, 8, 5), null, terminalActive: active)
            .AssessOpenContextAsync(7, 1, "POS-1");

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkShiftErrorCodes.TerminalAlreadyHasOpenShift, result.ErrorCode);
        Assert.False(result.Data!.BlockingWorkShift!.IsOwnedByRequester);
        Assert.Equal(status == WorkShiftStatuses.Open
                ? WorkShiftRecommendedActions.SwitchCurrentOperator
                : status == WorkShiftStatuses.Closing
                    ? WorkShiftRecommendedActions.CompleteClosing
                    : WorkShiftRecommendedActions.CountAndClose,
            result.Data.RecommendedAction);
    }

    private static WorkShiftService CreateService(
        DateTimeOffset now,
        StaffShift? schedule,
        WorkShift? staffActive = null,
        WorkShift? terminalActive = null)
    {
        var shifts = new Mock<IWorkShiftRepository>();
        shifts.Setup(x => x.EnsurePosTerminalAsync("POS-1", 1, "POS-1"))
            .Returns(Task.CompletedTask);
        shifts.Setup(x => x.GetEffectiveStaffShiftAsync(7, 1, It.IsAny<DateTime>()))
            .ReturnsAsync(schedule);
        shifts.Setup(x => x.GetActiveShiftAsync(7, 1)).ReturnsAsync(staffActive);
        shifts.Setup(x => x.GetActiveShiftByTerminalAsync(It.IsAny<string>(), 1)).ReturnsAsync(terminalActive);

        return new WorkShiftService(
            shifts.Object,
            Mock.Of<IPOSOrderRepository>(),
            Mock.Of<IOtpChallengeRepository>(),
            Mock.Of<CafeChain.Application.Interfaces.POS.IOtpPayloadFingerprintService>(),
            NullLogger<WorkShiftService>.Instance,
            workShiftOptions: Options.Create(new WorkShiftOptions()),
            timeProvider: new FixedTimeProvider(now));
    }

    private static WorkShift ActiveShift(string status, int staffId, string terminalId) => new()
    {
        ShiftId = 42,
        StoreId = 1,
        UserId = staffId,
        PosTerminalId = terminalId,
        Status = status,
        StartTimeUtc = DateTime.UtcNow.AddHours(-1),
        AutoCloseAtUtc = DateTime.UtcNow.AddHours(5)
    };

    private static StaffShift Schedule(DateTime workDate, int startHour, int endHour) => new()
    {
        StaffShiftId = 99,
        StaffId = 7,
        ShiftId = 1,
        WorkDate = workDate,
        Shift = new Shift
        {
            ShiftId = 1,
            StoreId = 1,
            Name = "Ca kiểm thử",
            StartTime = TimeSpan.FromHours(startHour),
            EndTime = TimeSpan.FromHours(endHour),
            IsOvernight = endHour <= startHour
        }
    };

    private static DateTimeOffset LocalUtc(int year, int month, int day, int hour, int minute)
    {
        var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        var zone = new WorkShiftOptions().ResolveTimeZone();
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
