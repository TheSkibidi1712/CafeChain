namespace CafeChain.Infrastructure.Configurations;

public sealed class AIImageOptions
{
    public const string SectionName = "AIImage";
    public bool PreferOnlineSource { get; set; } = true;
    public bool FallbackToComfyUI { get; set; } = false;
}

public sealed class AIImagePipelineOptions
{
    public const string SectionName = "AIImagePipeline";

    public int MaximumQueries { get; set; } = 6;
    public int MaximumSearchRounds { get; set; } = 3;
    public int PexelsResultsPerQuery { get; set; } = 15;
    public int MaximumCandidates { get; set; } = 3;
    public double MinimumCandidateScore { get; set; } = 0.60;
    public double PreferredCandidateScore { get; set; } = 0.75;
    public int MinimumImageWidth { get; set; } = 640;
    public int MinimumImageHeight { get; set; } = 640;
    public int CacheMinutes { get; set; } = 30;
    public int RetryCount { get; set; } = 2;
    public int RetryDelayMilliseconds { get; set; } = 350;
    public int GeneratedOutputCount { get; set; } = 3;
    public int GeneratedWidth { get; set; } = 1024;
    public int GeneratedHeight { get; set; } = 1024;
    public double DefaultDenoise { get; set; } = 0.55;
    public bool AllowTextOnlyFallback { get; set; } = true;
    public bool RequireTextFallbackConfirmation { get; set; } = true;
    public Dictionary<string, AIImageEntityProfileOptions> Entities { get; set; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Drink"] = new() { SubjectType = "beverage", Orientation = "square", Denoise = 0.55 },
            ["Topping"] = new() { SubjectType = "food ingredient", Orientation = "square", Denoise = 0.50 }
        };
}

public sealed class AIImageEntityProfileOptions
{
    public string SubjectType { get; set; } = string.Empty;
    public string Orientation { get; set; } = "square";
    public double? Denoise { get; set; }
}
