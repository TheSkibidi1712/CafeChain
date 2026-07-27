using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Staffs;

public sealed class SaveAvailabilityRuleDto
{
    public int StaffId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}
public sealed class SaveWorkConstraintDto
{
    public int StaffId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    [Range(0, 168)] public decimal TargetWeeklyHours { get; set; }
    [Range(1, 168)] public decimal MaxWeeklyHours { get; set; }
    [Range(1, 24)] public decimal MaxDailyHours { get; set; }
    [Range(0, 1440)] public int MinimumRestMinutes { get; set; } = 480;
}
public sealed class SaveStaffingRequirementDto
{
    public int StoreId { get; set; }
    public int ShiftId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    [Range(0, 100)] public int MinimumStaff { get; set; }
    [Range(0, 100)] public int TargetStaff { get; set; }
    [Range(0, 100)] public int MaximumStaff { get; set; }
    public int? RequiredRoleId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}
public sealed class SaveTimeOffDto
{
    public int StaffId { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    [Required, StringLength(500)] public string Reason { get; set; } = string.Empty;
}
public sealed class ShiftOptimizationSetupDto
{
    public int StoreId { get; set; }
    public List<ShiftOptimizationOptionDto> Staffs { get; set; } = [];
    public List<ShiftOptimizationOptionDto> Shifts { get; set; } = [];
    public List<ShiftOptimizationOptionDto> Roles { get; set; } = [];
    public List<object> Availability { get; set; } = [];
    public List<object> Constraints { get; set; } = [];
    public List<object> Requirements { get; set; } = [];
    public List<object> TimeOffs { get; set; } = [];
}
public sealed record ShiftOptimizationOptionDto(int Id, string Name);
