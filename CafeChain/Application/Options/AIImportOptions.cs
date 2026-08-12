namespace CafeChain.Application.Options;

public sealed class AIImportOptions
{
    public const string SectionName = "AIImport";
    public long MaxFileBytes { get; set; } = 10 * 1024 * 1024;
    public long MaxExpandedBytes { get; set; } = 100 * 1024 * 1024;
    public decimal MaxCompressionRatio { get; set; } = 100m;
    public int MaxSheets { get; set; } = 20;
    public int MaxRowsPerSheet { get; set; } = 10_000;
    public int MaxTotalRows { get; set; } = 20_000;
    public int MaxColumnsPerSheet { get; set; } = 100;
    public int MaxTotalCells { get; set; } = 200_000;
    public int MaxRegionsPerSheet { get; set; } = 20;
    public int MaxAiSampleRows { get; set; } = 20;
    public int SessionLifetimeHours { get; set; } = 24;
    public int DefaultPageSize { get; set; } = 50;
    public int MaximumPageSize { get; set; } = 200;
    public decimal HighConfidenceThreshold { get; set; } = 0.90m;
    public decimal ReviewConfidenceThreshold { get; set; } = 0.70m;
}
