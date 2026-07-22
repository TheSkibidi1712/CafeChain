using CafeChain.Application.Interfaces.StaffHub;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.StaffHub;
using CafeChain.ViewModels.StaffHub;

namespace CafeChain.Application.Services.StaffHub;

public sealed class StaffScheduleService : IStaffScheduleService
{
    private readonly IStaffScheduleRepository _repository;
    public StaffScheduleService(IStaffScheduleRepository repository) => _repository = repository;

    public async Task<ServiceResult<StaffHubScheduleVM>> GetAsync(int staffId, DateTime selectedDate, CancellationToken ct = default)
    {
        var date = selectedDate.Date;
        var offset = (7 + date.DayOfWeek - DayOfWeek.Monday) % 7;
        var weekStart = date.AddDays(-offset);
        var staff = await _repository.GetStaffScheduleAsync(staffId, weekStart, weekStart.AddDays(6), ct);
        if (staff == null) return ServiceResult<StaffHubScheduleVM>.Failure("Không tìm thấy hồ sơ nhân viên đang hoạt động.");

        return ServiceResult<StaffHubScheduleVM>.Success(new StaffHubScheduleVM
        {
            StaffId = staff.StaffId,
            StaffName = staff.FullName,
            StoreName = staff.Store.Name,
            AvatarUrl = staff.AvatarUrl,
            RequiresPasswordChange = staff.Account.RequiresPasswordChange,
            WeekStart = weekStart,
            Schedules = staff.StaffShifts.OrderBy(x => x.WorkDate).ThenBy(x => x.CustomStartTime ?? x.Shift.StartTime)
                .Select(x =>
                {
                    var start = x.CustomStartTime ?? x.Shift.StartTime;
                    var end = x.CustomEndTime ?? x.Shift.EndTime;
                    return new StaffHubScheduleItemVM
                    {
                        StaffShiftId = x.StaffShiftId,
                        WorkDate = x.WorkDate,
                        ShiftName = x.Shift.Name,
                        StartTime = start,
                        EndTime = end,
                        IsOvernight = x.Shift.IsOvernight || end <= start,
                        StatusCode = x.Status.Code
                    };
                }).ToList()
        });
    }
}
