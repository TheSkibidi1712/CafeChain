namespace CafeChain.Application.Options;

public sealed class PosRecommendationOptions
{
    public const string SectionName = "PosRecommendation";
    public bool Enabled { get; set; }
    public int AnalysisWindowDays { get; set; } = 90;
    public int IntervalHours { get; set; } = 24;
    public int MinimumBasketCount { get; set; } = 30;
    public decimal MinimumSupport { get; set; } = .02m;
    public decimal MinimumConfidence { get; set; } = .15m;
    public decimal MinimumLift { get; set; } = 1.05m;
    public int MaximumResults { get; set; } = 3;
    public string ModelVersion { get; set; } = "basket-v1";
}

public sealed class AnomalyDetectionOptions
{
    public const string SectionName = "AnomalyDetection";
    public bool Enabled { get; set; }
    public int IntervalMinutes { get; set; } = 60;
    public int AnalysisWindowDays { get; set; } = 28;
    public int MinimumSampleCount { get; set; } = 14;
    public decimal MinimumAbsoluteRevenueDeviation { get; set; } = 500000m;
    public decimal MinimumPercentageDeviation { get; set; } = .25m;
    public decimal RobustScoreThreshold { get; set; } = 3.5m;
}

