using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Staffs;

namespace CafeChain.Models.Inventories.Procurement;

public class PurchaseOrderBatch
{
    public int PurchaseOrderBatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public string RequestKey { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Currency { get; set; } = "VND";
    public DateTime ExpectedDeliveryFrom { get; set; }
    public DateTime ExpectedDeliveryTo { get; set; }
    public string? Note { get; set; }
    public int CreatedByStaffId { get; set; }
    public int? ApprovedByStaffId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public int? CancelledByStaffId { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual Supplier Supplier { get; set; } = null!;
    public virtual Staff CreatedByStaff { get; set; } = null!;
    public virtual Staff? ApprovedByStaff { get; set; }
    public virtual Staff? CancelledByStaff { get; set; }
    public virtual ICollection<PurchaseOrderBatchLine> Lines { get; set; } = new List<PurchaseOrderBatchLine>();
    public virtual ICollection<PurchaseOrder> ChildPurchaseOrders { get; set; } = new List<PurchaseOrder>();
    public virtual ICollection<PurchaseOrderBatchDocumentRevision> DocumentRevisions { get; set; } = new List<PurchaseOrderBatchDocumentRevision>();
}

public class PurchaseOrderBatchLine
{
    public int PurchaseOrderBatchLineId { get; set; }
    public int PurchaseOrderBatchId { get; set; }
    public int IngredientId { get; set; }
    public int IngredientSupplierId { get; set; }
    public int PackageUnitId { get; set; }
    public decimal PackageQuantitySnapshot { get; set; }
    public decimal TotalPackageCount { get; set; }
    public decimal TotalBaseQuantity { get; set; }
    public decimal PackagePriceSnapshot { get; set; }
    public decimal LineTotal { get; set; }
    public string Currency { get; set; } = "VND";
    public string? Note { get; set; }

    public virtual PurchaseOrderBatch PurchaseOrderBatch { get; set; } = null!;
    public virtual Ingredient Ingredient { get; set; } = null!;
    public virtual IngredientSupplier IngredientSupplier { get; set; } = null!;
    public virtual Unit PackageUnit { get; set; } = null!;
    public virtual ICollection<PurchaseOrderLineAllocation> Allocations { get; set; } = new List<PurchaseOrderLineAllocation>();
}

public class PurchaseOrderLineAllocation
{
    public int PurchaseOrderLineAllocationId { get; set; }
    public int PurchaseAdviceLineId { get; set; }
    public int PurchaseOrderBatchLineId { get; set; }
    public int PurchaseOrderId { get; set; }
    public int PurchaseOrderLineId { get; set; }
    public decimal AllocatedBaseQuantity { get; set; }
    public decimal AllocatedPackageQuantity { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual PurchaseAdviceLine PurchaseAdviceLine { get; set; } = null!;
    public virtual PurchaseOrderBatchLine PurchaseOrderBatchLine { get; set; } = null!;
    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
    public virtual PurchaseOrderLine PurchaseOrderLine { get; set; } = null!;
}
