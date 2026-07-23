namespace CafeChain.Application.Options;

public sealed class DashboardIntelligenceOptions
{
    public const string SectionName = "DashboardIntelligence";
    public bool IntentParserEnabled { get; set; }
    public bool ExplanationEnabled { get; set; }
    public int AnalysisCacheMinutes { get; set; } = 10;
    public int MaximumPromptLength { get; set; } = 500;
    public int MaximumPeriodDays { get; set; } = 366;
    public int RequestsPerMinute { get; set; } = 20;
    public decimal RevenueDropPercent { get; set; } = 20;
    public decimal RevenueDropAmount { get; set; } = 1_000_000;
    public int MinimumOrderSample { get; set; } = 10;
    public decimal WasteIncreasePercent { get; set; } = 25;
    public decimal WasteIncreaseAmount { get; set; } = 100_000;
}
