namespace CafeChain.Infrastructure.Configurations;

public sealed class AIImageOptions
{
    public const string SectionName = "AIImage";
    public bool PreferOnlineSource { get; set; } = true;
    public bool FallbackToComfyUI { get; set; } = true;
}
