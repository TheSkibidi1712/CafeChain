namespace CafeChain.Infrastructure.Configurations;

public sealed class AIOptions
{
    public const string SectionName = "AI";
    public bool Enabled { get; set; } = true;
    public string Provider { get; set; } = "Ollama";
    public bool UseFallbackWhenUnavailable { get; set; } = true;
    public string SkillRootPath { get; set; } = "Resources/AI/skills";
    public string SchemaRootPath { get; set; } = "Resources/AI/schemas";
    public int MaximumSkillContextCharacters { get; set; } = 12000;
    public int SuggestionHistoryLimit { get; set; } = 30;
    public int SuggestionHistoryMinutes { get; set; } = 30;
    public double NearNameSimilarityThreshold { get; set; } = 0.86;
    public double CompositeSimilarityThreshold { get; set; } = 0.82;
    public int MinimumRelevanceScore { get; set; } = 65;
    public int StructuredResponseRetries { get; set; } = 2;
}
