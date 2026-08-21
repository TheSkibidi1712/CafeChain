namespace CafeChain.Application.DTOs.AI;

public sealed class PosRecommendationDto
{
    public int TriggerDrinkId { get; init; }
    public int RecommendedDrinkId { get; init; }
    public string DrinkName { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public decimal Price { get; init; }
    public decimal Support { get; init; }
    public decimal Confidence { get; init; }
    public decimal Lift { get; init; }
    public int Rank { get; init; }
}

public sealed class PosRecommendationResultDto
{
    public Guid RecommendationSessionId { get; init; }
    public string Variant { get; init; } = "CONTROL";
    public IReadOnlyList<PosRecommendationDto> Items { get; init; } = [];
}

public sealed class PosRecommendationInteractionDto
{
    public Guid RecommendationSessionId { get; init; }
    public int TriggerDrinkId { get; init; }
    public int RecommendedDrinkId { get; init; }
    public string Action { get; init; } = string.Empty;
}

public sealed class OperationalAnomalyDto
{
    public int Id { get; init; }
    public int StoreId { get; init; }
    public string MetricCode { get; init; } = string.Empty;
    public string PeriodKey { get; init; } = string.Empty;
    public DateTime BusinessDate { get; init; }
    public string DetectionVersion { get; init; } = string.Empty;
    public decimal CurrentValue { get; init; }
    public decimal BaselineValue { get; init; }
    public decimal PercentageDeviation { get; init; }
    public decimal RobustScore { get; init; }
    public string Severity { get; init; } = string.Empty;
    public string Confidence { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<string> ReasonCodes { get; init; } = [];
    public string MetricDisplayName { get; init; } = string.Empty;
    public string CurrentValueDisplay { get; init; } = string.Empty;
    public string BaselineValueDisplay { get; init; } = string.Empty;
    public string DeviationDisplay { get; init; } = string.Empty;
    public string SeverityDisplay { get; init; } = string.Empty;
    public string StatusDisplay { get; init; } = string.Empty;
    public string ConfidenceDisplay { get; init; } = string.Empty;
    public IReadOnlyList<string> ReasonSummaries { get; init; } = [];
    public IReadOnlyList<string> SuggestedChecks { get; init; } = [];
    public string FeedbackDisplay { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class AnomalyFeedbackDto
{
    public int Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? Feedback { get; init; }
    public string? Note { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class AnomalyFeedbackResultDto
{
    public int Id { get; init; }
    public string Feedback { get; init; } = string.Empty;
    public string? Note { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public DateTime UpdatedAtUtc { get; init; }
    public string FeedbackDisplay { get; init; } = string.Empty;
}

public sealed class ForecastExplanationContextDto
{
    public long RunId { get; init; }
    public string ModelType { get; init; } = string.Empty;
    public DateTime TrainingToExclusive { get; init; }
    public decimal PointForecast { get; init; }
    public decimal LowerBound { get; init; }
    public decimal UpperBound { get; init; }
    public decimal Wape { get; init; }
    public string QualityStatus { get; init; } = string.Empty;
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed class SupplierExplanationContextDto
{
    public int SupplierId { get; init; }
    public decimal TotalScore { get; init; }
    public IReadOnlyDictionary<string, decimal> ComponentScores { get; init; } = new Dictionary<string, decimal>();
    public string Confidence { get; init; } = string.Empty;
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed class AnomalyExplanationContextDto
{
    public int AnomalyId { get; init; }
    public string MetricCode { get; init; } = string.Empty;
    public decimal CurrentValue { get; init; }
    public decimal BaselineValue { get; init; }
    public decimal RobustScore { get; init; }
    public IReadOnlyList<string> ReasonCodes { get; init; } = [];
    public string MetricDisplayName { get; init; } = string.Empty;
    public string CurrentValueDisplay { get; init; } = string.Empty;
    public string BaselineValueDisplay { get; init; } = string.Empty;
    public string DirectionDescription { get; init; } = string.Empty;
    public IReadOnlyList<string> ReasonSummaries { get; init; } = [];
    public IReadOnlyList<string> SuggestedChecks { get; init; } = [];
    public decimal PercentageDeviation { get; init; }
    public decimal AbsolutePercentageDeviation { get; init; }
    public string PercentageDeviationDisplay { get; init; } = string.Empty;
    public string ImpactSummary { get; init; } = string.Empty;
    public string WhyDetected { get; init; } = string.Empty;
    public IReadOnlyList<string> ImmediateActions { get; init; } = [];
    public IReadOnlyList<string> PreparationChecklist { get; init; } = [];
}

public sealed class TypedExplanationResultDto
{
    public bool Success { get; init; }
    public string Explanation { get; init; } = string.Empty;
    public bool UsedOllama { get; init; }
    public bool UsedFallback { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
