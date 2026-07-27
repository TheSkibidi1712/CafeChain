using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Dashboard;

public static class DashboardIntentVersions
{
    public const string V1 = "v1";
    public const string V2 = "v2";
}

public enum DashboardPeriodType { Today, Yesterday, LastNDays, ThisWeek, LastWeek, ThisMonth, LastMonth, Custom }
public enum DashboardComparison { None, PreviousPeriod, PreviousWeek, PreviousMonth, PreviousYear }
public enum DashboardChartType { Kpi, Line, Bar, HorizontalBar, Donut, StackedBar, Heatmap, Scatter, Table }
public enum DashboardStoreSelectorMode { AllowedScope, NamedStore }
public enum DashboardBusinessIntent
{
    RevenueAnalysis,
    SalesTrend,
    OrderAnalysis,
    ProductPerformance,
    StoreComparison,
    InventoryAnalysis,
    ReorderAnalysis,
    SupplierAnalysis,
    AnomalyDetection,
    GeneralBusinessSummary,
    StatisticsRequest
}

public sealed class DashboardPromptRequestDto
{
    [StringLength(500)] public string Prompt { get; set; } = string.Empty;
    public string Locale { get; set; } = "vi-VN";
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? StoreId { get; set; }
    public Guid? ContextId { get; set; }
}

public sealed class DashboardPeriodDto
{
    public DashboardPeriodType Type { get; set; } = DashboardPeriodType.LastNDays;
    [Range(1, 366)] public int? Value { get; set; } = 7;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public sealed class DashboardStoreSelectorDto
{
    public DashboardStoreSelectorMode Mode { get; set; } = DashboardStoreSelectorMode.AllowedScope;
    [StringLength(200)] public string? StoreName { get; set; }
}

public sealed class DashboardIntentDto
{
    public string IntentVersion { get; set; } = DashboardIntentVersions.V2;
    public DashboardBusinessIntent BusinessIntent { get; set; } = DashboardBusinessIntent.GeneralBusinessSummary;
    public List<string> FocusMetrics { get; set; } = [];
    // Kept for v1 API compatibility. In v2 the server derives this primary widget.
    public DashboardAnalyticsWidget Widget { get; set; }
    public DashboardPeriodDto Period { get; set; } = new();
    public DashboardComparison Comparison { get; set; }
    [RegularExpression("^(Hour|Day|Week|Month)$")] public string Granularity { get; set; } = "Day";
    [Range(1, 100)] public int Top { get; set; } = 10;
    public DashboardStoreSelectorDto StoreSelector { get; set; } = new();
    public DashboardChartType Chart { get; set; } = DashboardChartType.Table;
}

public sealed class DashboardIntentParseResultDto
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public DashboardIntentDto? Intent { get; set; }
    public bool UsedOllama { get; set; }
    public bool UsedFallback { get; set; }
    public List<string> Warnings { get; set; } = [];
}

public sealed class DashboardComparisonResultDto
{
    public decimal CurrentValue { get; set; }
    public decimal? BaselineValue { get; set; }
    public decimal? AbsoluteDifference { get; set; }
    public decimal? PercentageDifference { get; set; }
    public long CurrentSampleSize { get; set; }
    public long BaselineSampleSize { get; set; }
}

public sealed class DashboardInsightDto
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "INFO";
    public string Message { get; set; } = string.Empty;
    public decimal? CurrentValue { get; set; }
    public decimal? BaselineValue { get; set; }
    public decimal? DeviationPercent { get; set; }
}

public sealed class DashboardChartDto
{
    public DashboardChartType Type { get; set; }
    public string WidgetKey { get; set; } = string.Empty;
    public DashboardSection Section { get; set; }
    public string Title { get; set; } = string.Empty;
    public string XField { get; set; } = string.Empty;
    public string YField { get; set; } = string.Empty;
    public string ValueField { get; set; } = string.Empty;
    public string SeriesField { get; set; } = string.Empty;
    public string XUnit { get; set; } = string.Empty;
    public string YUnit { get; set; } = string.Empty;
    public int MinimumRows { get; set; } = 1;
    public IReadOnlyDictionary<string, string> FieldLabels { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public object Rows { get; set; } = Array.Empty<object>();
}

public sealed class DashboardAnalysisResultDto
{
    public Guid AnalysisId { get; set; }
    public DashboardIntentDto Intent { get; set; } = new();
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public IReadOnlyList<int> StoreIds { get; set; } = [];
    public string DataStatus { get; set; } = "AVAILABLE";
    public DashboardComparisonResultDto Comparison { get; set; } = new();
    public DashboardChartDto Chart { get; set; } = new();
    public List<DashboardInsightDto> Insights { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class DashboardInsightExplanationContextDto
{
    public Guid AnalysisId { get; set; }
    public DashboardAnalyticsWidget Widget { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public DashboardComparisonResultDto Comparison { get; set; } = new();
    public IReadOnlyList<DashboardInsightDto> Insights { get; set; } = [];
    public DashboardBusinessIntent BusinessIntent { get; set; }
    public IReadOnlyList<DashboardEvidenceDto> Evidence { get; set; } = [];
    public DashboardAnalysisContextDto? Context { get; set; }
    public IReadOnlyList<DashboardChartAnalysisDto> ChartAnalyses { get; set; } = [];
}

public sealed class DashboardExplanationResultDto
{
    public bool Success { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<DashboardNarrativeItemDto> Inferences { get; set; } = [];
    public List<DashboardNarrativeItemDto> Recommendations { get; set; } = [];
    public List<DashboardNarrativeItemDto> Overview { get; set; } = [];
    public List<DashboardNarrativeItemDto> NotablePoints { get; set; } = [];
    public List<DashboardNarrativeItemDto> Conclusions { get; set; } = [];
    public List<DashboardChartAnalysisDto> ChartAnalyses { get; set; } = [];
    public bool UsedOllama { get; set; }
    public bool UsedFallback { get; set; }
    public List<string> Warnings { get; set; } = [];
}

public sealed class DashboardDataPeriodResultDto
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public DateTime? ComparisonFrom { get; set; }
    public DateTime? ComparisonTo { get; set; }
}

public sealed class DashboardEvidenceDto
{
    public string EvidenceId { get; set; } = string.Empty;
    public string Kind { get; set; } = "FACT";
    public DashboardAnalyticsWidget SourceWidget { get; set; }
    public string WidgetKey { get; set; } = string.Empty;
    public string SectionKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Statement { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public decimal CurrentValue { get; set; }
    public decimal? BaselineValue { get; set; }
    public decimal? Delta { get; set; }
    public decimal? DeviationPercent { get; set; }
    public long SampleSize { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string DataStatus { get; set; } = "Complete";
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? EntityCode { get; set; }
    public string? EntityName { get; set; }
    public int? StoreId { get; set; }
    public string? StoreName { get; set; }
    public string? Baseline { get; set; }
    public string? Priority { get; set; }
    public string? RiskLevel { get; set; }
    public Dictionary<string, object?> Metadata { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class DashboardNarrativeItemDto
{
    public string Text { get; set; } = string.Empty;
    public List<string> EvidenceIds { get; set; } = [];
    public string? Priority { get; set; }
    public string? VerifyCondition { get; set; }
}

public sealed class DashboardAnomalyResultDto
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "INFO";
    public string Message { get; set; } = string.Empty;
    public List<string> EvidenceIds { get; set; } = [];
}

public sealed class DashboardStructuredAnalysisResultDto
{
    public Guid AnalysisId { get; set; }
    public DashboardBusinessIntent Intent { get; set; }
    public DashboardDataPeriodResultDto DataPeriod { get; set; } = new();
    public IReadOnlyList<int> StoreIds { get; set; } = [];
    public IReadOnlyList<DashboardStoreOptionDto> Stores { get; set; } = [];
    public string FilterFingerprint { get; set; } = string.Empty;
    public string DataStatus { get; set; } = "Insufficient";
    public string Summary { get; set; } = string.Empty;
    public List<DashboardEvidenceDto> Facts { get; set; } = [];
    public List<DashboardNarrativeItemDto> Inferences { get; set; } = [];
    public List<DashboardEvidenceDto> Statistics { get; set; } = [];
    public List<DashboardAnomalyResultDto> Anomalies { get; set; } = [];
    public List<DashboardNarrativeItemDto> Recommendations { get; set; } = [];
    public decimal Confidence { get; set; }
    public List<DashboardChartDto> Charts { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public string AiStatus { get; set; } = "Fallback";
    public string? FallbackReason { get; set; }
    public bool UsedFallback { get; set; }
    public List<DashboardSectionTelemetryDto> SectionTelemetry { get; set; } = [];
    public DashboardAnalysisContextDto? Context { get; set; }
    public List<DashboardChartAnalysisDto> ChartAnalyses { get; set; } = [];
    public List<DashboardNarrativeItemDto> Overview { get; set; } = [];
    public List<DashboardNarrativeItemDto> NotablePoints { get; set; } = [];
    public List<DashboardNarrativeItemDto> Conclusions { get; set; } = [];
}

public sealed class DashboardChartAnalysisDto
{
    public DashboardAnalyticsWidget Widget { get; set; }
    public DashboardSection Section { get; set; }
    public string Title { get; set; } = string.Empty;
    public DashboardChartType ChartType { get; set; }
    public string DataStatus { get; set; } = "Insufficient";
    public string Summary { get; set; } = string.Empty;
    public string Trend { get; set; } = "Insufficient";
    public decimal? CurrentValue { get; set; }
    public decimal? BaselineValue { get; set; }
    public decimal? PercentageDifference { get; set; }
    public bool ComparisonAvailable { get; set; }
    public string? HighestPoint { get; set; }
    public string? LowestPoint { get; set; }
    public List<string> Facts { get; set; } = [];
    public List<string> Anomalies { get; set; } = [];
    public List<string> Highlights { get; set; } = [];
    public List<DashboardEntityContributionDto> TopEntities { get; set; } = [];
    public List<DashboardEvidenceDto> Evidence { get; set; } = [];
    public DashboardChartDto Chart { get; set; } = new();
}

public sealed class DashboardEntityContributionDto
{
    public string Entity { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal? ContributionPercent { get; set; }
}
