namespace CafeChain.Infrastructure.Configurations;

public sealed class PexelsOptions
{
    public const string SectionName = "Pexels";
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "https://api.pexels.com";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 20;
    public int PerPage { get; set; } = 12;
    public int MaxImageBytes { get; set; } = 5 * 1024 * 1024;
}
