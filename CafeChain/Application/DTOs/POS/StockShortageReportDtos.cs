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
    }

    public class StockShortageReportResultDto
    {
        public int StockAlertId { get; set; }
        public string CreatedOrUpdated { get; set; } = string.Empty;
        public int NotificationCount { get; set; }
        public bool EmailAttempted { get; set; }
        public int EmailSentCount { get; set; }
        public int EmailFailedCount { get; set; }
        public List<string> Warnings { get; set; } = new();
    }
}
