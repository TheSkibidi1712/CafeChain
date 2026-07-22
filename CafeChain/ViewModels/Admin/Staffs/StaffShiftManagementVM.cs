using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Admin.Staffs;

public sealed class StaffShiftManagementVM
{
    public int StoreId { get; init; }
    public string StoreName { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public IReadOnlyList<StaffShiftStoreOptionVM> Stores { get; init; } = Array.Empty<StaffShiftStoreOptionVM>();
    public IReadOnlyList<StaffScheduleRowVM> StaffRows { get; init; } = Array.Empty<StaffScheduleRowVM>();
    public IReadOnlyList<ShiftTemplateVM> Templates { get; init; } = Array.Empty<ShiftTemplateVM>();
    public bool CanCreate { get; init; }
    public bool CanUpdate { get; init; }
    public bool CanCancel { get; init; }
}

public sealed record StaffShiftStoreOptionVM(int StoreId, string StoreName);

public sealed class StaffScheduleRowVM
{
    public int StaffId { get; init; }
    public string StaffName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public string RoleNames { get; init; } = string.Empty;
    public IReadOnlyList<StaffScheduleItemVM> Schedules { get; init; } = Array.Empty<StaffScheduleItemVM>();
}

public sealed class StaffScheduleItemVM
{
    public int StaffShiftId { get; init; }
    public int ShiftId { get; init; }
    public string ShiftName { get; init; } = string.Empty;
    public DateTime WorkDate { get; init; }
    public TimeSpan EffectiveStart { get; init; }
    public TimeSpan EffectiveEnd { get; init; }
    public TimeSpan? CustomStartTime { get; init; }
    public TimeSpan? CustomEndTime { get; init; }
    public bool IsOvernight { get; init; }
    public string StatusCode { get; init; } = string.Empty;
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ShiftTemplateVM
{
    public int ShiftId { get; init; }
    public string Name { get; init; } = string.Empty;
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
    public bool IsOvernight { get; init; }
    public bool Active { get; init; }
    public string? Notes { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public class AssignStaffShiftRequest
{
    public int StaffId { get; set; }
    public int ShiftId { get; set; }
    public DateTime WorkDate { get; set; }
    public bool UseCustomTime { get; set; }
    public TimeSpan? CustomStartTime { get; set; }
    public TimeSpan? CustomEndTime { get; set; }
}

public sealed class UpdateStaffShiftRequest : AssignStaffShiftRequest
{
    public int StaffShiftId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class CancelStaffShiftRequest
{
    public int StaffShiftId { get; set; }
    [Required, StringLength(500)] public string Reason { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public class CreateShiftTemplateRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
}

public sealed class UpdateShiftTemplateRequest : CreateShiftTemplateRequest
{
    public int ShiftId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ToggleShiftTemplateRequest
{
    public int ShiftId { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}
