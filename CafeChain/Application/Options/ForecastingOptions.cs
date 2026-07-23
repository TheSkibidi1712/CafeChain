namespace CafeChain.Application.Options;

public sealed class ForecastingOptions
{
    public const string SectionName = "Forecasting";
    public bool RevenueEnabled { get; set; }
    public bool ProductEnabled { get; set; }
    public int AnalysisWindowDays { get; set; } = 180;
    public int RevenueMinimumDays { get; set; } = 56;
    public int ProductMinimumDays { get; set; } = 84;
    public decimal ProductMinimumActiveDayRatio { get; set; } = 0.30m;
    public int[] Horizons { get; set; } = [7, 30];
    public int WorkerIntervalHours { get; set; } = 24;
    public int ResultTtlDays { get; set; } = 3;
}

public sealed class SupplierIntelligenceOptions
{
    public const string SectionName = "SupplierIntelligence";
    public bool ScoringEnabled { get; set; }
    public string WeightVersion { get; set; } = "v1";
    public decimal PriceWeight { get; set; } = 30;
    public decimal OnTimeWeight { get; set; } = 20;
    public decimal FillWeight { get; set; } = 20;
    public decimal QualityWeight { get; set; } = 20;
    public decimal LeadTimeWeight { get; set; } = 10;
    public int MediumConfidenceReceipts { get; set; } = 5;
    public int HighConfidenceReceipts { get; set; } = 20;
}
