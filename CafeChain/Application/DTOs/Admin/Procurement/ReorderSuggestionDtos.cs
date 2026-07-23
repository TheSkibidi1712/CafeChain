namespace CafeChain.Application.DTOs.Admin.Procurement
{
    public sealed class ReorderSuggestionListDto
    {
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public int AnalysisWindowDays { get; set; }
        public DateTime CalculatedAtUtc { get; set; }
        public List<ReorderSuggestionItemDto> Items { get; set; } = new();
    }

    public sealed class ReorderSuggestionItemDto
    {
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public int IngredientId { get; set; }
        public string IngredientCode { get; set; } = string.Empty;
        public string IngredientName { get; set; } = string.Empty;
        public string BaseUnitCode { get; set; } = string.Empty;
        public decimal AvailableQuantity { get; set; }
        public decimal ReservedQuantity { get; set; }
        public decimal UsableQuantity { get; set; }
        public decimal ProjectedQuantity { get; set; }
        public decimal? MinLevel { get; set; }
        public decimal? AverageDailyUsage { get; set; }
        public int? LeadTimeDays { get; set; }
        public decimal IncomingApprovedPoQuantity { get; set; }
        public decimal PendingPurchaseAdviceQuantity { get; set; }
        public decimal? ReorderPoint { get; set; }
        public decimal? SuggestedBaseQuantity { get; set; }
        public int? SuggestedPackageCount { get; set; }
        public int? MinimumOrderPackageCount { get; set; }
        public decimal? PackageBaseQuantity { get; set; }
        public int? IngredientSupplierId { get; set; }
        public int? SupplierId { get; set; }
        public string? SupplierCode { get; set; }
        public string? SupplierName { get; set; }
        public decimal? PackagePrice { get; set; }
        public decimal? EstimatedAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string RecommendationLevel { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public int? ActiveRestockRequestId { get; set; }
    }
}
