namespace CafeChain.Application.DTOs.POS
{
    public class StockShortageReportRequestDto
    {
        /// <summary>Preferred: resolves store identity and inventory snapshot.</summary>
        public int? StoreInventoryId { get; set; }

        public int? IngredientId { get; set; }

        public int? RecipeId { get; set; }

        /// <summary>Required report note (latest note on alert).</summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>
        /// Required business reason when usable stock is at/above the minimum threshold,
        /// or when no threshold is configured.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Explicit target in the inventory item's canonical base UOM.
        /// Mutually exclusive with ForecastDemandUntilDeliveryBaseQuantity.
        /// </summary>
        public decimal? TargetStockBaseQuantity { get; set; }

        /// <summary>
        /// Additional demand expected before replenishment arrives, in canonical base UOM.
        /// The service normalizes this to target = current usable + forecast.
        /// </summary>
        public decimal? ForecastDemandUntilDeliveryBaseQuantity { get; set; }
    }

    public class StockShortageReportResultDto
    {
        public int StockAlertId { get; set; }
        public string CreatedOrUpdated { get; set; } = string.Empty;
        public int NotificationCount { get; set; }
        public bool EmailAttempted { get; set; }
        public int EmailSentCount { get; set; }
        public int EmailFailedCount { get; set; }
        public string AlertType { get; set; } = string.Empty;
        public bool IsOutOfThresholdDemand { get; set; }
        public decimal AvailableBaseQuantity { get; set; }
        public decimal? MinimumThresholdBaseQuantity { get; set; }
        public decimal? DecisionTargetBaseQuantity { get; set; }
        public decimal SuggestedBaseQuantity { get; set; }
        public List<string> Warnings { get; set; } = new();
    }
}
