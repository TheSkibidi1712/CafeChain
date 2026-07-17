namespace CafeChain.Application.Interfaces.AI;

public sealed record AISkillContext(
    string EntityType,
    IReadOnlyList<string> LoadedFiles,
    string Content,
    string? SuggestionSchema,
    IReadOnlyList<string> Warnings);

public interface IAISkillCatalog
{
    Task<AISkillContext> GetContextAsync(
        string entityType,
        bool includeImageSkills,
        CancellationToken cancellationToken = default);
}

public interface IAISuggestionHistoryStore
{
    IReadOnlyList<string> Get(string entityType);
    void Add(string entityType, IEnumerable<string> suggestions);
}
