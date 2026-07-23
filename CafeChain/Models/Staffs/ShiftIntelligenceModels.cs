using CafeChain.Models.Permissions;
using CafeChain.Models.Stores;
using CafeChain.Models.Analytics;

namespace CafeChain.Models.Staffs;

public class StaffAvailabilityRule
{
    public long StaffAvailabilityRuleId { get; set; }
    public int StaffId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool Active { get; set; } = true;
    public int CreatedByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public virtual Staff Staff { get; set; } = null!;
    public virtual Staff CreatedByStaff { get; set; } = null!;
}

public class StaffAvailabilityException
{
    public long StaffAvailabilityExceptionId { get; set; }
    public int StaffId { get; set; }
    public DateTime Date { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public bool IsAvailable { get; set; }
    public string? Reason { get; set; }
    public int CreatedByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public virtual Staff Staff { get; set; } = null!;
    public virtual Staff CreatedByStaff { get; set; } = null!;
}

public class StaffTimeOff
{
    public long StaffTimeOffId { get; set; }
    public int StaffId { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public string Status { get; set; } = "PENDING";
    public string Reason { get; set; } = string.Empty;
    public int RequestedByStaffId { get; set; }
    public int? ReviewedByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public virtual Staff Staff { get; set; } = null!;
    public virtual Staff RequestedByStaff { get; set; } = null!;
    public virtual Staff? ReviewedByStaff { get; set; }
}

public class StaffWorkConstraint
{
    public long StaffWorkConstraintId { get; set; }
    public int StaffId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public decimal TargetWeeklyHours { get; set; }
    public decimal MaxWeeklyHours { get; set; }
    public decimal MaxDailyHours { get; set; }
    public int MinimumRestMinutes { get; set; } = 480;
    public int CreatedByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public virtual Staff Staff { get; set; } = null!;
    public virtual Staff CreatedByStaff { get; set; } = null!;
}

public class StoreStaffingRequirement
{
    public long StoreStaffingRequirementId { get; set; }
    public int StoreId { get; set; }
    public int ShiftId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public int MinimumStaff { get; set; }
    public int TargetStaff { get; set; }
    public int MaximumStaff { get; set; }
    public int? RequiredRoleId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool Active { get; set; } = true;
    public int CreatedByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public virtual Store Store { get; set; } = null!;
    public virtual Shift Shift { get; set; } = null!;
    public virtual Role? RequiredRole { get; set; }
    public virtual Staff CreatedByStaff { get; set; } = null!;
}

public class ScheduleOptimizationProposal
{
    public Guid ScheduleOptimizationProposalId { get; set; }
    public int StoreId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string ConstraintVersion { get; set; } = "heuristic-v1";
    public long? ForecastRunId { get; set; }
    public string Status { get; set; } = "DRAFT";
    public string ScoreBreakdownJson { get; set; } = "{}";
    public string ViolationsJson { get; set; } = "[]";
    public int CreatedByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? AppliedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public virtual Store Store { get; set; } = null!;
    public virtual Staff CreatedByStaff { get; set; } = null!;
    public virtual ForecastRun? ForecastRun { get; set; }
    public virtual ICollection<ScheduleOptimizationAssignment> Assignments { get; set; } = new List<ScheduleOptimizationAssignment>();
}

public class ScheduleOptimizationAssignment
{
    public long ScheduleOptimizationAssignmentId { get; set; }
    public Guid ScheduleOptimizationProposalId { get; set; }
    public int StaffId { get; set; }
    public int ShiftId { get; set; }
    public DateTime WorkDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string ReasonCodesJson { get; set; } = "[]";
    public virtual ScheduleOptimizationProposal Proposal { get; set; } = null!;
    public virtual Staff Staff { get; set; } = null!;
    public virtual Shift Shift { get; set; } = null!;
}
