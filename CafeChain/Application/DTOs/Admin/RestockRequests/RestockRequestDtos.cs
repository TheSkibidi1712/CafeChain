namespace CafeChain.Application.DTOs.Admin.RestockRequests
{
    public class RestockRequestListItemDto
    {
        public int RestockRequestId { get; set; }
        public int? StockAlertId { get; set; }
        public int StoreId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemTypeLabel { get; set; } = string.Empty;
        public decimal RequestedQuantity { get; set; }
        public decimal? SuggestedQuantity { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class RestockRequestDetailDto : RestockRequestListItemDto
    {
        public int? IngredientId { get; set; }
        public int? RecipeId { get; set; }
        /// <summary>Issue #122 — stable BTP identity when present.</summary>
        public int? PreparedItemId { get; set; }
        public int CreatedByStaffId { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? StoreName { get; set; }
        public string? AlertType { get; set; }
        public string? AlertStatus { get; set; }
        public decimal? AlertCurrentQtySnapshot { get; set; }
        public decimal? AlertThresholdSnapshot { get; set; }
        public int? SuggestionAnalysisWindowDays { get; set; }
        public decimal? SuggestionAvailableSnapshot { get; set; }
        public decimal? SuggestionMinLevelSnapshot { get; set; }
        public decimal? SuggestionAverageDailyUsageSnapshot { get; set; }
        public int? SuggestionLeadTimeDaysSnapshot { get; set; }
        public decimal? SuggestionIncomingQuantitySnapshot { get; set; }
        public string? SuggestionReason { get; set; }
    }

    public class RestockRequestListResultDto
    {
        public int StoreId { get; set; }
        public string? StatusFilter { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public List<RestockRequestListItemDto> Items { get; set; } = new();
    }

    public class CreateRestockRequestResultDto
    {
        public int RestockRequestId { get; set; }
        public bool AlreadyExisted { get; set; }
        public bool NotifiedAccountantWarehouse { get; set; }
        public int RecipientCount { get; set; }
    }
}
