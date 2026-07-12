namespace CafeChain.Infrastructure.Configurations;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";
    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 120;
    public string KeepAlive { get; set; } = "5m";
    public decimal Temperature { get; set; } = 0.2m;
    public bool Think { get; set; }
}
