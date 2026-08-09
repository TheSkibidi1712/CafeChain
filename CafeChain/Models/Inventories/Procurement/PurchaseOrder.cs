using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using CafeChain.Models.Enums.Inventory;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Models.Inventories.Procurement
{
    public class PurchaseOrder
    {
        public int PurchaseOrderId { get; set; }
        public int? PurchaseOrderBatchId { get; set; }
        public int? MasterPurchaseOrderId { get; set; }
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

        public virtual Store Store { get; set; } = null!;
        public virtual Supplier Supplier { get; set; } = null!;
        public virtual Staff CreatedByStaff { get; set; } = null!;
        public virtual Staff? ApprovedByStaff { get; set; }
        public virtual Staff? SentByStaff { get; set; }
        public virtual ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
        public virtual PurchaseOrderBatch? PurchaseOrderBatch { get; set; }
        public virtual PurchaseOrder? MasterPurchaseOrder { get; set; }
        public virtual ICollection<PurchaseOrder> ChildPurchaseOrders { get; set; } = new List<PurchaseOrder>();
        public virtual ICollection<PurchaseOrderLineAllocation> BatchAllocations { get; set; } = new List<PurchaseOrderLineAllocation>();
    }

    public class PurchaseOrderLine
    {
        public int PurchaseOrderLineId { get; set; }
        public int PurchaseOrderId { get; set; }
        public int? RestockRequestId { get; set; }
        public int? PurchaseAdviceLineId { get; set; }
        public int IngredientId { get; set; }
        public int IngredientSupplierId { get; set; }
        public int? PackageUnitIdSnapshot { get; set; }
        public decimal? PackageQuantitySnapshot { get; set; }
        public decimal? PackagePriceSnapshot { get; set; }
        public decimal? PackageCount { get; set; }
        public PurchaseMode PurchaseMode { get; set; } = PurchaseMode.Packaged;
        public decimal? OrderedPackageCount { get; set; }
        public decimal OrderedBaseQuantity { get; set; }
        public decimal? OrderedPackQuantity { get; set; }
        public decimal? PackSizeProcurementQuantity { get; set; }
        public int? ProcurementUnitId { get; set; }
        public decimal? OrderedProcurementQuantity { get; set; }
        public decimal? UnitPricePerPackage { get; set; }
        public decimal? UnitPricePerProcurementUnit { get; set; }
        public decimal? RoundingSurplusProcurementQuantity { get; set; }
        public decimal? AcceptedPackQuantity { get; set; }
        public decimal? AcceptedProcurementQuantity { get; set; }
        public decimal ClosedProcurementQuantity { get; set; }
        public decimal? InventoryPostingBaseQuantity { get; set; }
        public int? InventoryBaseUnitId { get; set; }
        public decimal? ProcurementToInventoryFactor { get; set; }
        public decimal ClosedRemainingQuantity { get; set; }
        public string? CloseRemainingReason { get; set; }
        public int? ClosedRemainingByStaffId { get; set; }
        public DateTime? ClosedRemainingAtUtc { get; set; }
        public int PromisedLeadTimeDaysSnapshot { get; set; }
        public string? Note { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
        public virtual RestockRequest? RestockRequest { get; set; }
        public virtual PurchaseAdviceLine? PurchaseAdviceLine { get; set; }
        public virtual Ingredient Ingredient { get; set; } = null!;
        public virtual IngredientSupplier IngredientSupplier { get; set; } = null!;
        public virtual Unit? PackageUnitSnapshot { get; set; }
        public virtual Unit? ProcurementUnit { get; set; }
        public virtual Unit? InventoryBaseUnit { get; set; }
        public virtual Staff? ClosedRemainingByStaff { get; set; }
        public virtual ICollection<PurchaseOrderReceiptPosting> ReceiptPostings { get; set; } = new List<PurchaseOrderReceiptPosting>();
        public virtual ICollection<PurchaseOrderLineClosure> Closures { get; set; } = new List<PurchaseOrderLineClosure>();
        public virtual ICollection<PurchaseOrderLineAllocation> BatchAllocations { get; set; } = new List<PurchaseOrderLineAllocation>();
    }

    public class PurchaseOrderLineClosure
    {
        public long PurchaseOrderLineClosureId { get; set; }
        public int PurchaseOrderLineId { get; set; }
        public decimal ClosedBaseQuantity { get; set; }
        public decimal? ClosedProcurementQuantity { get; set; }
        public int? ProcurementUnitId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string RequestKey { get; set; } = string.Empty;
        public string PayloadHash { get; set; } = string.Empty;
        public int ActorStaffId { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public virtual PurchaseOrderLine PurchaseOrderLine { get; set; } = null!;
        public virtual Unit? ProcurementUnit { get; set; }
        public virtual Staff ActorStaff { get; set; } = null!;
    }

    public class PurchaseOrderReceiptPosting
    {
        public int PurchaseOrderReceiptPostingId { get; set; }
        public int PurchaseOrderLineId { get; set; }
        public int BranchReceiptLineId { get; set; }
        public decimal AcceptedBaseQuantity { get; set; }
        public decimal RejectedBaseQuantity { get; set; }
        public decimal? AcceptedProcurementQuantity { get; set; }
        public decimal? RejectedProcurementQuantity { get; set; }
        public decimal? InventoryPostingBaseQuantity { get; set; }
        public int? ProcurementUnitId { get; set; }
        public int? InventoryBaseUnitId { get; set; }
        public decimal? ProcurementToInventoryFactor { get; set; }
        public PurchaseMode PurchaseMode { get; set; } = PurchaseMode.Packaged;
        public int CreatedByStaffId { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public virtual PurchaseOrderLine PurchaseOrderLine { get; set; } = null!;
        public virtual BranchReceiptLine BranchReceiptLine { get; set; } = null!;
        public virtual Staff CreatedByStaff { get; set; } = null!;
        public virtual Unit? ProcurementUnit { get; set; }
        public virtual Unit? InventoryBaseUnit { get; set; }
    }
}
