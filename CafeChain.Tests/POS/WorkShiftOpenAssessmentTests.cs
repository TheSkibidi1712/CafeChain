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
        Assert.False(result.Data.ApprovalRequired);
        Assert.True(result.Data.ManagerApprovalRequired);
        Assert.Equal(31, result.Data.MinutesLate);
    }

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(15, false, false)]
    [InlineData(16, true, false)]
    [InlineData(30, true, false)]
    [InlineData(31, true, true)]
    public async Task Late_open_boundaries_follow_business_policy(
        int minutesLate,
        bool reasonRequired,
        bool managerApprovalRequired)
    {
        var result = await CreateService(
                LocalUtc(2026, 8, 3, 8, minutesLate),
                Schedule(new DateTime(2026, 8, 3), 8, 16))
            .AssessOpenShiftAsync(7, 1, new OpenShiftAssessmentRequestDto { PosTerminalId = "POS-1" });

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(minutesLate, result.Data!.MinutesLate);
        Assert.Equal(reasonRequired, result.Data.ReasonRequired);
        Assert.False(result.Data.ApprovalRequired);
        Assert.Equal(managerApprovalRequired, result.Data.ManagerApprovalRequired);
    }

    [Fact]
    public async Task Schedule_past_end_grace_cannot_be_bound_again()
    {
        var result = await CreateService(
                LocalUtc(2026, 8, 3, 16, 31),
                Schedule(new DateTime(2026, 8, 3), 8, 16))
            .AssessOpenShiftAsync(7, 1, new OpenShiftAssessmentRequestDto { PosTerminalId = "POS-1" });

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(WorkShiftOpenContexts.OutsideSchedule, result.Data!.OpenContext);
        Assert.Null(result.Data.SourceStaffShiftId);
        Assert.True(result.Data.ApprovalRequired);
        Assert.False(result.Data.ManagerApprovalRequired);
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

    [Fact]
    public async Task Resume_legacy_shift_binds_selected_terminal_before_issuing_context()
    {
        var active = ActiveShift(WorkShiftStatuses.Open, staffId: 7, terminalId: null!);
        var shifts = new Mock<IWorkShiftRepository>();
        shifts.Setup(x => x.GetActiveShiftAsync(7, 1)).ReturnsAsync(active);
        shifts.Setup(x => x.BindTerminalForResumeAsync(42, 7, 1, "POS-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                active.PosTerminalId = "POS-1";
                return active;
            });

        var result = await CreateService(shifts.Object)
            .PrepareResumeExchangeContextAsync(70, 7, 1, "POS-1");

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("POS-1", result.Data!.TerminalId);
        Assert.Equal(42, result.Data.WorkShiftId);
        shifts.VerifyAll();
    }

    [Fact]
    public async Task Resume_rejects_terminal_mismatch_from_atomic_binding()
    {
        var active = ActiveShift(WorkShiftStatuses.Open, staffId: 7, terminalId: "POS-OLD");
        var shifts = new Mock<IWorkShiftRepository>();
        shifts.Setup(x => x.GetActiveShiftAsync(7, 1)).ReturnsAsync(active);
        shifts.Setup(x => x.BindTerminalForResumeAsync(42, 7, 1, "POS-NEW", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CafeChain.Application.Results.WorkShiftBusinessException(
                WorkShiftErrorCodes.WorkShiftTerminalMismatch,
                "Phiên POS hiện tại đang thuộc terminal khác."));

        var result = await CreateService(shifts.Object)
            .PrepareResumeExchangeContextAsync(70, 7, 1, "POS-NEW");

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkShiftErrorCodes.WorkShiftTerminalMismatch, result.ErrorCode);
        shifts.VerifyAll();
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

    private static WorkShiftService CreateService(IWorkShiftRepository shifts) => new(
        shifts,
        Mock.Of<IPOSOrderRepository>(),
        Mock.Of<IOtpChallengeRepository>(),
        Mock.Of<CafeChain.Application.Interfaces.POS.IOtpPayloadFingerprintService>(),
        NullLogger<WorkShiftService>.Instance);

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
