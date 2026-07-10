namespace CafeChain.Application.DTOs.Admin.StockAlerts
{
    public class StockAlertListItemDto
    {
        public int StockAlertId { get; set; }
        public int StoreId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemTypeLabel { get; set; } = string.Empty;
        public string AlertType { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public decimal CurrentQtySnapshot { get; set; }
        public decimal? ThresholdSnapshot { get; set; }
        public string? ReporterNote { get; set; }
        public string? ReporterName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class StockAlertDetailDto : StockAlertListItemDto
    {
        public int? IngredientId { get; set; }
        public int? RecipeId { get; set; }
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
