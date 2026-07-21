using CafeChain.Application.Services.Admin.Staffs;
using CafeChain.Infrastructure.Interfaces.Admin.Staffs;
using CafeChain.Models.Staffs;
using CafeChain.ViewModels.Admin.Staffs;
using Moq;

namespace CafeChain.Tests;

public sealed class ShiftSchedulingV13Tests
{
    private static readonly StaffShiftStatus Scheduled = new() { StaffShiftStatusId = 1, Code = "SCHEDULED", Name = "Đã lên lịch" };
    private static readonly StaffShiftStatus Cancelled = new() { StaffShiftStatusId = 2, Code = "CANCELLED", Name = "Đã hủy" };

    [Fact]
    public async Task Overnight_schedule_blocks_overlap_on_next_work_date()
    {
        var repository = BaseRepository();
        var staff = ActiveStaff();
        var target = Template(2, TimeSpan.FromHours(5), TimeSpan.FromHours(10));
        var overnight = Template(1, TimeSpan.FromHours(22), TimeSpan.FromHours(6));
        overnight.IsOvernight = true;
        repository.Setup(x => x.GetStaffAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(staff);
        repository.Setup(x => x.GetTemplateAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        repository.Setup(x => x.GetPotentialOverlapsAsync(10, new DateTime(2026, 1, 10), new DateTime(2026, 1, 12), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StaffShift { StaffShiftId = 1, StaffId = 10, ShiftId = 1, WorkDate = new DateTime(2026, 1, 10), Shift = overnight, Status = Scheduled }]);

        var result = await new AdminStaffShiftService(repository.Object).AssignAsync(1, 99, new AssignStaffShiftRequest
        {
            StaffId = 10, ShiftId = 2, WorkDate = new DateTime(2026, 1, 11)
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("SHIFT_OVERLAP", result.ErrorCode);
        repository.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Adjacent_schedule_is_allowed_and_saved_as_scheduled()
    {
        var repository = BaseRepository();
        var staff = ActiveStaff();
        var target = Template(2, TimeSpan.FromHours(6), TimeSpan.FromHours(10));
        var overnight = Template(1, TimeSpan.FromHours(22), TimeSpan.FromHours(6));
        overnight.IsOvernight = true;
        repository.Setup(x => x.GetStaffAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(staff);
        repository.Setup(x => x.GetTemplateAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        repository.Setup(x => x.GetPotentialOverlapsAsync(10, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StaffShift { StaffShiftId = 1, StaffId = 10, ShiftId = 1, WorkDate = new DateTime(2026, 1, 10), Shift = overnight, Status = Scheduled }]);
        repository.Setup(x => x.GetScheduleAsync(10, 2, new DateTime(2026, 1, 11), It.IsAny<CancellationToken>())).ReturnsAsync((StaffShift?)null);
        repository.Setup(x => x.GetStatusAsync("SCHEDULED", It.IsAny<CancellationToken>())).ReturnsAsync(Scheduled);
        repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await new AdminStaffShiftService(repository.Object).AssignAsync(1, 99, new AssignStaffShiftRequest
        {
            StaffId = 10, ShiftId = 2, WorkDate = new DateTime(2026, 1, 11)
        });

        Assert.True(result.IsSuccess, result.Message);
        repository.Verify(x => x.Add(It.Is<StaffShift>(s => s.StatusId == 1 && s.CustomStartTime == null && s.CustomEndTime == null)), Times.Once);
        repository.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancel_requires_reason_and_keeps_record_with_cancelled_status()
    {
        var repository = BaseRepository();
        var version = new byte[] { 1, 2, 3 };
        var shift = Template(1, TimeSpan.FromHours(8), TimeSpan.FromHours(12));
        var schedule = new StaffShift
        {
            StaffShiftId = 7, StaffId = 10, ShiftId = 1, WorkDate = new DateTime(2026, 1, 10),
            RowVersion = version, Shift = shift, Staff = ActiveStaff(), Status = Scheduled, StatusId = 1
        };
        repository.Setup(x => x.GetScheduleAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(schedule);
        repository.Setup(x => x.GetStatusAsync("CANCELLED", It.IsAny<CancellationToken>())).ReturnsAsync(Cancelled);
        repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new AdminStaffShiftService(repository.Object);
        var noReason = await service.CancelAsync(1, 99, new CancelStaffShiftRequest { StaffShiftId = 7, RowVersion = Convert.ToBase64String(version) });
        var result = await service.CancelAsync(1, 99, new CancelStaffShiftRequest { StaffShiftId = 7, Reason = "Nhân viên xin nghỉ", RowVersion = Convert.ToBase64String(version) });

        Assert.False(noReason.IsSuccess);
        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(2, schedule.StatusId);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Inactive_template_cannot_be_assigned()
    {
        var repository = BaseRepository();
        var template = Template(2, TimeSpan.FromHours(8), TimeSpan.FromHours(12));
        template.Active = false;
        repository.Setup(x => x.GetStaffAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(ActiveStaff());
        repository.Setup(x => x.GetTemplateAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(template);

        var result = await new AdminStaffShiftService(repository.Object).AssignAsync(1, 99, new AssignStaffShiftRequest
        {
            StaffId = 10, ShiftId = 2, WorkDate = new DateTime(2026, 1, 12)
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("INACTIVE_SHIFT", result.ErrorCode);
        repository.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Staff_outside_selected_store_is_forbidden()
    {
        var repository = BaseRepository();
        var staff = ActiveStaff();
        staff.StoreId = 2;
        repository.Setup(x => x.GetStaffAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(staff);
        repository.Setup(x => x.GetTemplateAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Template(1, TimeSpan.FromHours(8), TimeSpan.FromHours(12)));

        var result = await new AdminStaffShiftService(repository.Object).AssignAsync(1, 99, new AssignStaffShiftRequest
        {
            StaffId = 10, ShiftId = 1, WorkDate = new DateTime(2026, 1, 12)
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
        repository.Verify(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancelled_matching_schedule_is_restored_instead_of_inserted_again()
    {
        var repository = BaseRepository();
        var template = Template(1, TimeSpan.FromHours(8), TimeSpan.FromHours(12));
        var existing = new StaffShift
        {
            StaffShiftId = 7,
            StaffId = 10,
            ShiftId = 1,
            WorkDate = new DateTime(2026, 1, 12),
            Shift = template,
            Staff = ActiveStaff(),
            Status = Cancelled,
            StatusId = Cancelled.StaffShiftStatusId
        };
        repository.Setup(x => x.GetStaffAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(existing.Staff);
        repository.Setup(x => x.GetTemplateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        repository.Setup(x => x.GetPotentialOverlapsAsync(10, It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        repository.Setup(x => x.GetStatusAsync("SCHEDULED", It.IsAny<CancellationToken>())).ReturnsAsync(Scheduled);
        repository.Setup(x => x.GetScheduleAsync(10, 1, new DateTime(2026, 1, 12), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await new AdminStaffShiftService(repository.Object).AssignAsync(1, 99, new AssignStaffShiftRequest
        {
            StaffId = 10, ShiftId = 1, WorkDate = new DateTime(2026, 1, 12)
        });

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(Scheduled.StaffShiftStatusId, existing.StatusId);
        repository.Verify(x => x.Add(It.IsAny<StaffShift>()), Times.Never);
        repository.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Updating_with_stale_row_version_returns_concurrency_conflict()
    {
        var repository = BaseRepository();
        var template = Template(1, TimeSpan.FromHours(8), TimeSpan.FromHours(12));
        var schedule = new StaffShift
        {
            StaffShiftId = 7,
            StaffId = 10,
            ShiftId = 1,
            WorkDate = new DateTime(2026, 1, 12),
            RowVersion = [1],
            Shift = template,
            Staff = ActiveStaff(),
            Status = Scheduled,
            StatusId = Scheduled.StaffShiftStatusId
        };
        repository.Setup(x => x.GetScheduleAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(schedule);
        repository.Setup(x => x.GetTemplateAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(template);

        var result = await new AdminStaffShiftService(repository.Object).UpdateAssignmentAsync(1, 99, new UpdateStaffShiftRequest
        {
            StaffShiftId = 7,
            StaffId = 10,
            ShiftId = 1,
            WorkDate = new DateTime(2026, 1, 12),
            RowVersion = Convert.ToBase64String([2])
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("CONCURRENCY_CONFLICT", result.ErrorCode);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IAdminStaffShiftRepository> BaseRepository()
    {
        var repository = new Mock<IAdminStaffShiftRepository>(MockBehavior.Loose);
        repository.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repository.Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repository.Setup(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return repository;
    }

    private static Staff ActiveStaff() => new() { StaffId = 10, StoreId = 1, Active = true, EmployeeStatus = 2, FullName = "Nhân viên" };

    private static Shift Template(int id, TimeSpan start, TimeSpan end) => new()
    {
        ShiftId = id, StoreId = 1, Name = $"Ca {id}", StartTime = start, EndTime = end,
        IsOvernight = end <= start, Active = true, RowVersion = [1]
    };
}
