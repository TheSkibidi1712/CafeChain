namespace CafeChain.Application.DTOs.Admin.StockAlerts
{
    public class StockAlertListItemDto
    {
        public int StockAlertId { get; set; }
        public int StoreId { get; set; }
        public int? IngredientId { get; set; }
        public int? RecipeId { get; set; }
        public int? PreparedItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemTypeLabel { get; set; } = string.Empty;
        public string AlertType { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public decimal CurrentQtySnapshot { get; set; }
        public decimal? ThresholdSnapshot { get; set; }
        public bool HasCurrentInventory { get; set; }
        public decimal? OnHandQty { get; set; }
        public decimal? ReservedQty { get; set; }
        public decimal? AvailableQty { get; set; }
        public string? ReporterNote { get; set; }
        public string? ReporterName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class StockAlertDetailDto : StockAlertListItemDto
    {
        public int? ReportedByStaffId { get; set; }
        public DateTime? ReportedAt { get; set; }
        public int? ConfirmedByStaffId { get; set; }
        public string? ConfirmedByName { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public string? ManagerNote { get; set; }
        public int? RejectedByStaffId { get; set; }
        public string? RejectedByName { get; set; }
        public DateTime? RejectedAt { get; set; }
        public string? RejectReason { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedReason { get; set; }
        public List<StockAlertTransitionDto> Transitions { get; set; } = new();
        public List<StockAlertMovementDto> RecentMovements { get; set; } = new();
    }

    public class StockAlertTransitionDto
    {
        public int StockAlertTransitionId { get; set; }
        public string? PreviousStatus { get; set; }
        public string NewStatus { get; set; } = string.Empty;
        public string? PreviousAlertType { get; set; }
        public string NewAlertType { get; set; } = string.Empty;
        public string? PreviousSeverity { get; set; }
        public string NewSeverity { get; set; } = string.Empty;
        public decimal OnHandSnapshot { get; set; }
        public decimal ReservedSnapshot { get; set; }
        public decimal AvailableSnapshot { get; set; }
        public decimal? MinLevelSnapshot { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public int? SourceId { get; set; }
        public string? Reason { get; set; }
        public int? ActorStaffId { get; set; }
        public string? ActorName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class StockAlertMovementDto
    {
        public int InventoryTransactionId { get; set; }
        public string Type { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal BeforeQty { get; set; }
        public decimal AfterQty { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? InventoryDocumentId { get; set; }
        public int? InventoryTransferId { get; set; }
        public int? ReferenceOrderId { get; set; }
        public int? ProductionRunId { get; set; }
        public int? BranchReceiptLineId { get; set; }
    }

    public class StockAlertListResultDto
    {
        public int StoreId { get; set; }
        public string? StatusFilter { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public List<StockAlertListItemDto> Items { get; set; } = new();
    }
}
