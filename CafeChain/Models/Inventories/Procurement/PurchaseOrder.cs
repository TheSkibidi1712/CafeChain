using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Models.Inventories.Procurement
{
    public sealed class PurchaseOrder
    {
        public int PurchaseOrderId { get; set; }
        public string Code { get; set; } = string.Empty;
        public int StoreId { get; set; }
        public int SupplierId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public DateTime? ExpectedDeliveryAtUtc { get; set; }
        public int CreatedByStaffId { get; set; }
        public int? ApprovedByStaffId { get; set; }
        public int? SentByStaffId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime? ApprovedAtUtc { get; set; }
        public DateTime? SentAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? CancelledAtUtc { get; set; }
        public string? Note { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public Store Store { get; set; } = null!;
        public Supplier Supplier { get; set; } = null!;
        public Staff CreatedByStaff { get; set; } = null!;
        public Staff? ApprovedByStaff { get; set; }
        public Staff? SentByStaff { get; set; }
        public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
    }

    public sealed class PurchaseOrderLine
    {
        public int PurchaseOrderLineId { get; set; }
        public int PurchaseOrderId { get; set; }
        public int? RestockRequestId { get; set; }
        public int IngredientId { get; set; }
        public int IngredientSupplierId { get; set; }
        public int PackageUnitIdSnapshot { get; set; }
        public decimal PackageQuantitySnapshot { get; set; }
        public decimal PackagePriceSnapshot { get; set; }
        public decimal PackageCount { get; set; }
        public decimal OrderedBaseQuantity { get; set; }
        public int PromisedLeadTimeDaysSnapshot { get; set; }
        public string? Note { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public PurchaseOrder PurchaseOrder { get; set; } = null!;
        public RestockRequest? RestockRequest { get; set; }
        public Ingredient Ingredient { get; set; } = null!;
        public IngredientSupplier IngredientSupplier { get; set; } = null!;
        public Unit PackageUnitSnapshot { get; set; } = null!;
        public ICollection<PurchaseOrderReceiptPosting> ReceiptPostings { get; set; } = new List<PurchaseOrderReceiptPosting>();
    }

    public sealed class PurchaseOrderReceiptPosting
    {
        public int PurchaseOrderReceiptPostingId { get; set; }
        public int PurchaseOrderLineId { get; set; }
        public int BranchReceiptLineId { get; set; }
        public decimal AcceptedBaseQuantity { get; set; }
        public decimal RejectedBaseQuantity { get; set; }
        public int CreatedByStaffId { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public PurchaseOrderLine PurchaseOrderLine { get; set; } = null!;
        public BranchReceiptLine BranchReceiptLine { get; set; } = null!;
        public Staff CreatedByStaff { get; set; } = null!;
    }
}
