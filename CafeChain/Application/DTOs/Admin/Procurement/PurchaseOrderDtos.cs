using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Application.DTOs.Admin.Procurement
{
    public sealed class CreatePurchaseOrderRequest
    {
        public int StoreId { get; set; }
        public int SupplierId { get; set; }
        public DateTime? ExpectedDeliveryAtUtc { get; set; }
        public string? Note { get; set; }
        public bool AllowOverallocationOverride { get; set; }
        public string? OverallocationOverrideReason { get; set; }
        public List<CreatePurchaseOrderLineRequest> Lines { get; set; } = new();
    }

    public sealed class CreatePurchaseOrderLineRequest
    {
        public int? PurchaseAdviceLineId { get; set; }
        public string? PurchaseAdviceLineRowVersion { get; set; }
        public int? RestockRequestId { get; set; }
        public string? RestockReferenceCode { get; set; }
        public int IngredientId { get; set; }
        public int IngredientSupplierId { get; set; }
        public PurchaseMode PurchaseMode { get; set; } = PurchaseMode.Packaged;
        public decimal? PackageCount { get; set; }
        public decimal? OrderedProcurementQuantity { get; set; }
        public int? ProcurementUnitId { get; set; }
        public string? Note { get; set; }
    }

    public sealed class PurchaseOrderDetailDto
    {
        public int PurchaseOrderId { get; set; }
        public string Code { get; set; } = string.Empty;
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string? StoreAddress { get; set; }
        public string? StorePhone { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? SupplierTaxCode { get; set; }
        public string? SupplierAddress { get; set; }
        public string? SupplierContactInfo { get; set; }
        public string? SupplierEmail { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public DateTime? ExpectedDeliveryAtUtc { get; set; }
        public string? Note { get; set; }
        public decimal TotalAmount { get; set; }
        public int CreatedByStaffId { get; set; }
        public string RowVersion { get; set; } = string.Empty;
        public int? ActiveReceiptDraftId { get; set; }
        public List<PurchaseOrderLineDto> Lines { get; set; } = new();
    }

    public sealed class PurchaseOrderLineDto
    {
        public PurchaseMode PurchaseMode { get; set; } = PurchaseMode.Packaged;
        public int PurchaseOrderLineId { get; set; }
        public int? RestockRequestId { get; set; }
        public string? RestockReferenceCode { get; set; }
        public int IngredientId { get; set; }
        public string IngredientName { get; set; } = string.Empty;
        public string BaseUnitName { get; set; } = string.Empty;
        public decimal? PackageCount { get; set; }
        public decimal? PackageQuantitySnapshot { get; set; }
        public string PackageUnitName { get; set; } = string.Empty;
        public decimal? PackagePriceSnapshot { get; set; }
        public decimal? UnitPricePerProcurementUnit { get; set; }
        public decimal OrderedBaseQuantity { get; set; }
        public decimal? OrderedProcurementQuantity { get; set; }
        public decimal? PackSizeProcurementQuantity { get; set; }
        public int? ProcurementUnitId { get; set; }
        public string? ProcurementUnitName { get; set; }
        public decimal? RoundingSurplusProcurementQuantity { get; set; }
        public decimal AcceptedBaseQuantity { get; set; }
        public decimal? AcceptedProcurementQuantity { get; set; }
        public decimal? InventoryPostingBaseQuantity { get; set; }
        public decimal RejectedBaseQuantity { get; set; }
        public decimal ClosedRemainingQuantity { get; set; }
        public string? CloseRemainingReason { get; set; }
        public int? ClosedRemainingByStaffId { get; set; }
        public DateTime? ClosedRemainingAtUtc { get; set; }
        public decimal RemainingBaseQuantity { get; set; }
        public decimal? RemainingProcurementQuantity { get; set; }
        public int ReceiptCount { get; set; }
        public string RowVersion { get; set; } = string.Empty;
        public int PromisedLeadTimeDaysSnapshot { get; set; }
    }

    public sealed class ClosePurchaseOrderLineRemainingRequest
    {
        public int PurchaseOrderLineId { get; set; }
        public string RowVersion { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string RequestKey { get; set; } = string.Empty;
    }

    public sealed class PurchaseOrderListItemDto
    {
        public int PurchaseOrderId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
