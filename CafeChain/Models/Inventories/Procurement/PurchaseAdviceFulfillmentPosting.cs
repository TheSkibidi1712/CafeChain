using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Staffs;

namespace CafeChain.Models.Inventories.Procurement;

public static class PurchaseAdviceFulfillmentPostingTypes
{
    public const string Accepted = "ACCEPTED";
    public const string Closed = "CLOSED";
}

public static class PurchaseAdviceFulfillmentSourceTypes
{
    public const string BranchReceiptLine = "BRANCH_RECEIPT_LINE";
    public const string PurchaseOrderCloseRemaining = "PO_CLOSE_REMAINING";
}

public class PurchaseAdviceFulfillmentPosting
{
    public long PurchaseAdviceFulfillmentPostingId { get; set; }
    public int PurchaseAdviceLineId { get; set; }
    public int PurchaseOrderLineAllocationId { get; set; }
    public int PurchaseOrderLineId { get; set; }
    public int? BranchReceiptLineId { get; set; }
    public string? CloseOperationKey { get; set; }
    public string PostingType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public int BaseUnitId { get; set; }
    public string SourceDocumentType { get; set; } = string.Empty;
    public int SourceDocumentId { get; set; }
    public int SourceDocumentLineId { get; set; }
    public string PayloadHash { get; set; } = string.Empty;
    public int ActorStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public virtual PurchaseAdviceLine PurchaseAdviceLine { get; set; } = null!;
    public virtual PurchaseOrderLineAllocation PurchaseOrderLineAllocation { get; set; } = null!;
    public virtual PurchaseOrderLine PurchaseOrderLine { get; set; } = null!;
    public virtual BranchReceiptLine? BranchReceiptLine { get; set; }
    public virtual Unit BaseUnit { get; set; } = null!;
    public virtual Staff ActorStaff { get; set; } = null!;
}
