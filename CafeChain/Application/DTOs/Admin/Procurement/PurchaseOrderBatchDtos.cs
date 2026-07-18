namespace CafeChain.Application.DTOs.Admin.Procurement;

public sealed class CreatePurchaseOrderBatchRequest : PurchaseAdviceConsolidationPreviewRequest
{
    public string RequestKey { get; set; } = string.Empty;
    public DateTime? ExpectedDeliveryFrom { get; set; }
    public DateTime? ExpectedDeliveryTo { get; set; }
    public string? Note { get; set; }
}

public sealed class PurchaseOrderBatchTransitionRequest
{
    public string RowVersion { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class PurchaseOrderBatchListItemDto
{
    public int PurchaseOrderBatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int StoreCount { get; set; }
    public DateTime ExpectedDeliveryFrom { get; set; }
    public DateTime ExpectedDeliveryTo { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class PurchaseOrderBatchDetailDto : PurchaseOrderBatchListItemDto
{
    public string Currency { get; set; } = "VND";
    public string? Note { get; set; }
    public int CreatedByStaffId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public int? ApprovedByStaffId { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public IReadOnlyList<PurchaseOrderBatchLineDto> Lines { get; set; } = Array.Empty<PurchaseOrderBatchLineDto>();
    public IReadOnlyList<PurchaseOrderBatchChildDto> ChildPurchaseOrders { get; set; } = Array.Empty<PurchaseOrderBatchChildDto>();
}

public sealed class PurchaseOrderBatchLineDto
{
    public int PurchaseOrderBatchLineId { get; set; }
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string PackageUnitName { get; set; } = string.Empty;
    public decimal PackageQuantitySnapshot { get; set; }
    public decimal TotalPackageCount { get; set; }
    public decimal TotalBaseQuantity { get; set; }
    public decimal PackagePriceSnapshot { get; set; }
    public decimal LineTotal { get; set; }
    public IReadOnlyList<PurchaseOrderBatchAllocationDto> Allocations { get; set; } = Array.Empty<PurchaseOrderBatchAllocationDto>();
}

public sealed class PurchaseOrderBatchAllocationDto
{
    public int PurchaseOrderLineAllocationId { get; set; }
    public int PurchaseAdviceLineId { get; set; }
    public int PurchaseAdviceId { get; set; }
    public string AdviceNumber { get; set; } = string.Empty;
    public int PurchaseOrderId { get; set; }
    public int PurchaseOrderLineId { get; set; }
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal AllocatedBaseQuantity { get; set; }
    public decimal AllocatedPackageQuantity { get; set; }
}

public sealed class PurchaseOrderBatchChildDto
{
    public int PurchaseOrderId { get; set; }
    public string Code { get; set; } = string.Empty;
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal OrderedBaseQuantity { get; set; }
    public decimal AcceptedBaseQuantity { get; set; }
    public decimal RemainingBaseQuantity { get; set; }
}

public sealed class PurchaseOrderBatchDetailPageDto
{
    public PurchaseOrderBatchDetailDto Batch { get; set; } = new();
    public IReadOnlyList<PurchaseOrderBatchDocumentRevisionDto> DocumentRevisions { get; set; } = Array.Empty<PurchaseOrderBatchDocumentRevisionDto>();
    public CafeChain.Application.DTOs.Admin.Actor.AdminActorContext Actor { get; set; } = new();
    public string ZaloMessage { get; set; } = string.Empty;
}
