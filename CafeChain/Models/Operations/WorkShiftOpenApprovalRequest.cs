using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Operations;

public class WorkShiftOpenApprovalRequest
{
    public int WorkShiftOpenApprovalRequestId { get; set; }
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public string RequestKey { get; set; } = string.Empty;
    public int StoreId { get; set; }
    public int RequestedByStaffId { get; set; }
    public int? DecidedByStaffId { get; set; }
    public int? SourceStaffShiftId { get; set; }
    public string TerminalId { get; set; } = string.Empty;
    public int MinutesLate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = WorkShiftOpenApprovalStatuses.Pending;
    public string? DecisionReason { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public virtual Store Store { get; set; } = null!;
    public virtual Staff RequestedByStaff { get; set; } = null!;
    public virtual Staff? DecidedByStaff { get; set; }
    public virtual StaffShift? SourceStaffShift { get; set; }
    public virtual PosTerminal Terminal { get; set; } = null!;
}

public static class WorkShiftOpenApprovalStatuses
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string ConvertedToOutsideSchedule = "CONVERTED_TO_OUTSIDE_SCHEDULE";
    public const string Cancelled = "CANCELLED";
    public const string Expired = "EXPIRED";
}
