using CafeChain.Models.Inventories.Documents;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories.Approvals;

public static class InventoryNegativeApprovalStatuses
{
    public const string Requested = "REQUESTED";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Cancelled = "CANCELLED";
}

public class InventoryNegativeApproval
{
    public long InventoryNegativeApprovalId { get; set; }
    public int InventoryDocumentId { get; set; }
    public int StoreId { get; set; }
    public int RequesterStaffId { get; set; }
    public int? ApproverStaffId { get; set; }
    public string Status { get; set; } = InventoryNegativeApprovalStatuses.Requested;
    public string Reason { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
    public string PolicyVersion { get; set; } = string.Empty;
    public string RequestKey { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public bool ScopeAuthorized { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public virtual InventoryDocument InventoryDocument { get; set; } = null!;
    public virtual Store Store { get; set; } = null!;
    public virtual Staff RequesterStaff { get; set; } = null!;
    public virtual Staff? ApproverStaff { get; set; }
    public virtual ICollection<InventoryNegativeApprovalLine> Lines { get; set; } = [];
}
