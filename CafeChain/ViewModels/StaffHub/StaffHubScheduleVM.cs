namespace CafeChain.ViewModels.StaffHub;

public sealed class StaffHubScheduleVM
{
    public int StaffId { get; init; }
    public string StaffName { get; init; } = string.Empty;
    public string StoreName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public bool RequiresPasswordChange { get; init; }
    public DateTime WeekStart { get; init; }
    public IReadOnlyList<StaffHubScheduleItemVM> Schedules { get; init; } = Array.Empty<StaffHubScheduleItemVM>();
}

public sealed class StaffHubScheduleItemVM
{
    public int StaffShiftId { get; init; }
    public DateTime WorkDate { get; init; }
    public string ShiftName { get; init; } = string.Empty;
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
    public bool IsOvernight { get; init; }
    public string StatusCode { get; init; } = string.Empty;
}
