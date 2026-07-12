namespace CafeChain.Infrastructure.Configurations;

public sealed class AIOptions
{
    public const string SectionName = "AI";
    public bool Enabled { get; set; } = true;
    public string Provider { get; set; } = "Ollama";
    public bool UseFallbackWhenUnavailable { get; set; } = true;
}
