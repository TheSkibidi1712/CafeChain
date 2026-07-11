namespace CafeChain.Application.DTOs.POS
{
    /// <summary>Issue #97 — summary of a stock-alert evaluation pass.</summary>
    public class StockAlertEvaluationResultDto
    {
        public int StoreId { get; set; }
        public int CreatedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int ResolvedCount { get; set; }
        public int SkippedUnconfiguredCount { get; set; }
        public int EvaluatedCount { get; set; }
        public string Source { get; set; } = string.Empty;
    }
}
