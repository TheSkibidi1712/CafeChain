namespace CafeChain.Models.Analytics;

/// <summary>
/// Store-scoped, non-PII telemetry used to evaluate controlled intelligence pilots.
/// </summary>
public class IntelligencePilotRun
{
    public long IntelligencePilotRunId { get; set; }
    public string FeatureCode { get; set; } = string.Empty;
    public int StoreId { get; set; }
    public string RunMode { get; set; } = "SHADOW";
    public DateTime StartedAtUtc { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public bool Success { get; set; }
    public string MetricsJson { get; set; } = "{}";
    public string? ErrorCategory { get; set; }
}
