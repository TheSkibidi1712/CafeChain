using CafeChain.Application.Constants;
using CafeChain.Models.Staffs;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Models.Inventories.Ice;

public class IceSupplementalIssue
{
    public int IceSupplementalIssueId { get; set; }
    public Guid PublicId { get; set; }
    public int IceAllocationId { get; set; }
    public decimal Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = IceSupplementalIssueStatuses.Pending;
    public int RequestedByStaffId { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public int? ApprovedByStaffId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public int? RejectedByStaffId { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
    public bool ReservationApplied { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public virtual IceAllocation IceAllocation { get; set; } = null!;
    public virtual Staff RequestedByStaff { get; set; } = null!;
    public virtual Staff? ApprovedByStaff { get; set; }
    public virtual Staff? RejectedByStaff { get; set; }
}
