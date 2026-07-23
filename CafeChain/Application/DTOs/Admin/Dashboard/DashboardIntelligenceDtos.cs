using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Dashboard;

public static class DashboardIntentVersions { public const string V1 = "v1"; }

public enum DashboardPeriodType { Today, Yesterday, LastNDays, ThisWeek, LastWeek, ThisMonth, LastMonth, Custom }
public enum DashboardComparison { None, PreviousPeriod, PreviousWeek, PreviousMonth, PreviousYear }
public enum DashboardChartType { Kpi, Line, Bar, StackedBar, Heatmap, Table }
public enum DashboardStoreSelectorMode { AllowedScope, NamedStore }

public sealed class DashboardPromptRequestDto
{
    [Required, StringLength(500, MinimumLength = 3)] public string Prompt { get; set; } = string.Empty;
    public string Locale { get; set; } = "vi-VN";
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
    public string IntentVersion { get; set; } = DashboardIntentVersions.V1;
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
    public string Title { get; set; } = string.Empty;
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
}

public sealed class DashboardExplanationResultDto
{
    public bool Success { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public bool UsedOllama { get; set; }
    public bool UsedFallback { get; set; }
    public List<string> Warnings { get; set; } = [];
}
