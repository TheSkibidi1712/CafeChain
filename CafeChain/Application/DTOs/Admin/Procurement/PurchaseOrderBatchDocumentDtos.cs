namespace CafeChain.Application.DTOs.Admin.Procurement;

public sealed class PurchaseOrderBatchDocumentRevisionDto
{
    public int RevisionId { get; set; }
    public int PurchaseOrderBatchId { get; set; }
    public int RevisionNumber { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public int GeneratedByStaffId { get; set; }
    public string GeneratedByName { get; set; } = string.Empty;
    public string? SentChannel { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public int? SentByStaffId { get; set; }
    public DateTime? SupersededAtUtc { get; set; }
    public int? SupersededByRevisionId { get; set; }
}

public sealed class PurchaseOrderBatchDocumentDownloadDto
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/pdf";
}

public sealed class MarkPurchaseOrderBatchDocumentSentRequest
{
    public string Channel { get; set; } = string.Empty;
}

public sealed class PurchaseOrderBatchDocumentSnapshot
{
    public string ContractVersion { get; set; } = "1";
    public int PurchaseOrderBatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = "VND";
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime? ApprovedAtUtc { get; set; }
    public string ApprovedByName { get; set; } = string.Empty;
    public DateTime ExpectedDeliveryFrom { get; set; }
    public DateTime ExpectedDeliveryTo { get; set; }
    public string? Note { get; set; }
    public PurchaseOrderBatchDocumentSupplierSnapshot Supplier { get; set; } = new();
    public IReadOnlyList<PurchaseOrderBatchDocumentLineSnapshot> Lines { get; set; } = Array.Empty<PurchaseOrderBatchDocumentLineSnapshot>();
    public IReadOnlyList<PurchaseOrderBatchDocumentStoreSnapshot> Stores { get; set; } = Array.Empty<PurchaseOrderBatchDocumentStoreSnapshot>();
    public decimal TotalAmount { get; set; }
}

public sealed class PurchaseOrderBatchDocumentSupplierSnapshot
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TaxCode { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
}

public sealed class PurchaseOrderBatchDocumentLineSnapshot
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string PackageUnitName { get; set; } = string.Empty;
    public decimal PackageQuantity { get; set; }
    public decimal PackageCount { get; set; }
    public decimal TotalBaseQuantity { get; set; }
    public decimal PackagePrice { get; set; }
    public decimal LineTotal { get; set; }
    public string? Note { get; set; }
}

public sealed class PurchaseOrderBatchDocumentStoreSnapshot
{
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string PurchaseOrderCode { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public DateTime? NeededByDate { get; set; }
    public string? Note { get; set; }
    public IReadOnlyList<PurchaseOrderBatchDocumentStoreLineSnapshot> Lines { get; set; } = Array.Empty<PurchaseOrderBatchDocumentStoreLineSnapshot>();
}

public sealed class PurchaseOrderBatchDocumentStoreLineSnapshot
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string PackageUnitName { get; set; } = string.Empty;
    public decimal PackageQuantity { get; set; }
    public decimal PackageCount { get; set; }
    public decimal BaseQuantity { get; set; }
    public DateTime NeededByDate { get; set; }
}
