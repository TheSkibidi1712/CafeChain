using CafeChain.Models.Stores;

namespace CafeChain.Models.Analytics;

public class ForecastRun
{
    public long ForecastRunId { get; set; }
    public string SeriesType { get; set; } = string.Empty;
    public int StoreId { get; set; }
    public int? EntityId { get; set; }
    public DateTime TrainingFrom { get; set; }
    public DateTime TrainingToExclusive { get; set; }
    public int HorizonDays { get; set; }
    public string ModelType { get; set; } = string.Empty;
    public string ModelVersion { get; set; } = "v1";
    public int SampleCount { get; set; }
    public decimal? Mae { get; set; }
    public decimal? Wape { get; set; }
    public string QualityStatus { get; set; } = string.Empty;
    public string WarningJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string InputDataVersion { get; set; } = string.Empty;
    public virtual Store Store { get; set; } = null!;
    public virtual ICollection<ForecastPoint> Points { get; set; } = new List<ForecastPoint>();
}

public class ForecastPoint
{
    public long ForecastPointId { get; set; }
    public long ForecastRunId { get; set; }
    public DateTime ForecastDate { get; set; }
    public decimal PointForecast { get; set; }
    public decimal LowerBound { get; set; }
    public decimal UpperBound { get; set; }
    public virtual ForecastRun ForecastRun { get; set; } = null!;
}
