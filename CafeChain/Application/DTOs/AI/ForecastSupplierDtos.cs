namespace CafeChain.Application.DTOs.AI;

public sealed record ForecastSeriesPointDto(DateTime Date, decimal Value);
public sealed record ForecastPointDto(DateTime Date, decimal PointForecast, decimal LowerBound, decimal UpperBound);

public sealed class ForecastResultDto
{
    public long ForecastRunId { get; set; }
    public string SeriesType { get; set; } = string.Empty;
    public int StoreId { get; set; }
    public int? EntityId { get; set; }
    public DateTime TrainingFrom { get; set; }
    public DateTime TrainingToExclusive { get; set; }
    public int HorizonDays { get; set; }
    public string ModelType { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = string.Empty;
    public int SampleCount { get; set; }
    public decimal? Mae { get; set; }
    public decimal? Wape { get; set; }
    public string QualityStatus { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = [];
    public List<ForecastPointDto> Points { get; set; } = [];
}

public sealed class SupplierScoreComponentDto
{
    public decimal Price { get; set; }
    public decimal OnTime { get; set; }
    public decimal Fill { get; set; }
    public decimal Quality { get; set; }
    public decimal LeadTime { get; set; }
}

public sealed class SupplierRecommendationCandidateDto
{
    public int SupplierId { get; set; }
    public int IngredientSupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string Confidence { get; set; } = string.Empty;
    public int PackageCount { get; set; }
    public decimal PackageBaseQuantity { get; set; }
    public decimal EstimatedAmount { get; set; }
    public SupplierScoreComponentDto ComponentScores { get; set; } = new();
    public List<string> Warnings { get; set; } = [];
}

public sealed class SupplierRecommendationDto
{
    public int StoreId { get; set; }
    public int IngredientId { get; set; }
    public decimal RequiredBaseQuantity { get; set; }
    public string WeightVersion { get; set; } = string.Empty;
    public DateTime CalculatedAtUtc { get; set; }
    public List<SupplierRecommendationCandidateDto> Candidates { get; set; } = [];
}
