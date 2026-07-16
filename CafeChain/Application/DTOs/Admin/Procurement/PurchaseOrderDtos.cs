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
        public int? RestockRequestId { get; set; }
        public int IngredientId { get; set; }
        public int IngredientSupplierId { get; set; }
        public decimal PackageCount { get; set; }
        public string? Note { get; set; }
    }

    public sealed class PurchaseOrderDetailDto
    {
        public int PurchaseOrderId { get; set; }
        public string Code { get; set; } = string.Empty;
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public DateTime? ExpectedDeliveryAtUtc { get; set; }
        public string? Note { get; set; }
        public decimal TotalAmount { get; set; }
        public string RowVersion { get; set; } = string.Empty;
        public List<PurchaseOrderLineDto> Lines { get; set; } = new();
    }

    public sealed class PurchaseOrderLineDto
    {
        public int PurchaseOrderLineId { get; set; }
        public int? RestockRequestId { get; set; }
        public int IngredientId { get; set; }
        public string IngredientName { get; set; } = string.Empty;
        public decimal PackageCount { get; set; }
        public decimal PackageQuantitySnapshot { get; set; }
        public string PackageUnitName { get; set; } = string.Empty;
        public decimal PackagePriceSnapshot { get; set; }
        public decimal OrderedBaseQuantity { get; set; }
        public decimal AcceptedBaseQuantity { get; set; }
        public decimal RejectedBaseQuantity { get; set; }
        public decimal RemainingBaseQuantity { get; set; }
        public int PromisedLeadTimeDaysSnapshot { get; set; }
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
