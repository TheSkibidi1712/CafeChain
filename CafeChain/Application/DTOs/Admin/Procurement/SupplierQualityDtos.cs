namespace CafeChain.Application.DTOs.Admin.Procurement;

public sealed class CreateSupplierReceiptIssueRequest
{
    public int BranchReceiptLineId { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public decimal AffectedBaseQuantity { get; set; }
    public string Description { get; set; } = string.Empty;
}

public sealed class SupplierReceiptIssueTransitionRequest
{
    public string TargetStatus { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class SupplierReceiptIssueListItemDto
{
    public int SupplierReceiptIssueId { get; set; }
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public int PurchaseOrderId { get; set; }
    public string PurchaseOrderCode { get; set; } = string.Empty;
    public int PurchaseOrderLineId { get; set; }
    public int BranchReceiptId { get; set; }
    public string BranchReceiptCode { get; set; } = string.Empty;
    public int BranchReceiptLineId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal AffectedBaseQuantity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ResolutionNote { get; set; }
    public string? DismissReason { get; set; }
    public string ReportedByName { get; set; } = string.Empty;
    public DateTime ReportedAtUtc { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class SupplierReceiptIssueContextDto
{
    public int BranchReceiptLineId { get; set; }
    public int BranchReceiptId { get; set; }
    public string BranchReceiptCode { get; set; } = string.Empty;
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int PurchaseOrderId { get; set; }
    public string PurchaseOrderCode { get; set; } = string.Empty;
    public int PurchaseOrderLineId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal AcceptedBaseQuantity { get; set; }
    public decimal RejectedBaseQuantity { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime? ExpectedDeliveryAtUtc { get; set; }
    public string? SuggestedIssueType { get; set; }
}

public sealed class CreateSupplierReceiptIssuePageDto
{
    public SupplierReceiptIssueContextDto Context { get; set; } = new();
    public CreateSupplierReceiptIssueRequest Input { get; set; } = new();
}

public sealed class SupplierPerformanceDto
{
    public int StoreId { get; set; }
    public int SupplierId { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public int CompletedDeliveryCount { get; set; }
    public int ConfirmedReceiptCount { get; set; }
    public int ExpectedDateSampleCount { get; set; }
    public decimal OnTimeRate { get; set; }
    public decimal FillRate { get; set; }
    public decimal RejectionRate { get; set; }
    public decimal IssueRate { get; set; }
    public decimal AverageDelayDays { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class SupplierQualityDashboardDto
{
    public int StoreId { get; set; }
    public int? SupplierId { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public SupplierPerformanceDto? Performance { get; set; }
    public List<SupplierReceiptIssueListItemDto> Issues { get; set; } = new();
}
