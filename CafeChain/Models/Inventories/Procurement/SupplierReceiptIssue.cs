using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Models.Inventories.Procurement;

public class SupplierReceiptIssue
{
    public int SupplierReceiptIssueId { get; set; }
    public int SupplierId { get; set; }
    public int StoreId { get; set; }
    public int PurchaseOrderId { get; set; }
    public int PurchaseOrderLineId { get; set; }
    public int BranchReceiptId { get; set; }
    public int BranchReceiptLineId { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal AffectedBaseQuantity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ResolutionNote { get; set; }
    public string? DismissReason { get; set; }
    public int ReportedByStaffId { get; set; }
    public int? ResolvedByStaffId { get; set; }
    public int? DismissedByStaffId { get; set; }
    public DateTime ReportedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime? DismissedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual Supplier Supplier { get; set; } = null!;
    public virtual Store Store { get; set; } = null!;
    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
    public virtual PurchaseOrderLine PurchaseOrderLine { get; set; } = null!;
    public virtual BranchReceipt BranchReceipt { get; set; } = null!;
    public virtual BranchReceiptLine BranchReceiptLine { get; set; } = null!;
    public virtual Staff ReportedByStaff { get; set; } = null!;
    public virtual Staff? ResolvedByStaff { get; set; }
    public virtual Staff? DismissedByStaff { get; set; }
    public virtual ICollection<SupplierReceiptIssueTransition> Transitions { get; set; } = new List<SupplierReceiptIssueTransition>();
}

public class SupplierReceiptIssueTransition
{
    public int SupplierReceiptIssueTransitionId { get; set; }
    public int SupplierReceiptIssueId { get; set; }
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public int ActorStaffId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }

    public virtual SupplierReceiptIssue SupplierReceiptIssue { get; set; } = null!;
    public virtual Staff ActorStaff { get; set; } = null!;
}
