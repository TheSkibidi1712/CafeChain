using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Settings;

public sealed class AIImportOcrSettingsDTO
{
    public bool InfrastructureConfigured { get; init; }
    public bool ProviderReady { get; init; }
    public bool EffectiveEnabled { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string? ProviderVersion { get; init; }
    public string Languages { get; init; } = "vie+eng";
    public bool ExecutableAvailable { get; init; }
    public bool ModelDataReady { get; init; }
    public decimal ReviewConfidenceThreshold { get; init; }
    public int RenderDpi { get; init; }
    public int MaxPages { get; init; }
    public long MaxRenderedPixelsPerPage { get; init; }
    public long MaxTotalRenderedPixels { get; init; }
    public int PageTimeoutSeconds { get; init; }
    public int TotalTimeoutSeconds { get; init; }
    public int MaxConcurrentPages { get; init; }
    public string ConfigVersion { get; init; } = string.Empty;
    public string HealthStatus { get; init; } = string.Empty;
    public string? HealthMessage { get; init; }
    public DateTime? LastHealthCheckedAtUtc { get; init; }
}

public sealed class UpdateAIImportOcrSettingsDTO
{
    [Required, StringLength(50)]
    [RegularExpression("^(vie\\+eng|vie|eng)$")]
    public string Languages { get; set; } = "vie+eng";
    [Range(typeof(decimal), "0", "1")] public decimal ReviewConfidenceThreshold { get; set; } = 0.85m;
    [Range(72, 600)] public int RenderDpi { get; set; } = 200;
    [Range(1, 500)] public int MaxPages { get; set; } = 50;
    [Range(1, long.MaxValue)] public long MaxRenderedPixelsPerPage { get; set; } = 20_000_000;
    [Range(1, long.MaxValue)] public long MaxTotalRenderedPixels { get; set; } = 200_000_000;
    [Range(1, 600)] public int PageTimeoutSeconds { get; set; } = 45;
    [Range(1, 3600)] public int TotalTimeoutSeconds { get; set; } = 180;
    [Range(1, 16)] public int MaxConcurrentPages { get; set; } = 1;
}
