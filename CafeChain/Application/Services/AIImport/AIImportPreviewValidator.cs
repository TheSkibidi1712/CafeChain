using System.Text.Json;
using CafeChain.Application.DTOs.AIImport;
using CafeChain.Models.AIImport;

namespace CafeChain.Application.Services.AIImport;

public sealed class AIImportResolutionEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public HashSet<int> ResolveScope(
        IReadOnlyList<(ImportGroup Group, ImportItem Item)> all,
        AIImportValidationScope scope)
    {
        if (scope.Kind == AIImportValidationScopeKind.Full)
            return all.Select(entry => entry.Item.ImportItemId).ToHashSet();

        var affected = new HashSet<int>();
        var entities = new HashSet<AIImportEntityType>();
        var keys = new HashSet<string>(StringComparer.Ordinal);

        if (scope.Kind == AIImportValidationScopeKind.Group && scope.GroupId.HasValue)
        {
            foreach (var entry in all.Where(entry => entry.Group.ImportGroupId == scope.GroupId.Value))
            {
                affected.Add(entry.Item.ImportItemId);
                entities.Add(entry.Group.EntityType);
                keys.Add(BusinessKey(entry));
            }
            if (scope.PreviousEntityType.HasValue) entities.Add(scope.PreviousEntityType.Value);
        }
        else if (scope.ItemId.HasValue)
        {
            var target = all.Single(entry => entry.Item.ImportItemId == scope.ItemId.Value);
            affected.Add(target.Item.ImportItemId);
            entities.Add(target.Group.EntityType);
            keys.Add(BusinessKey(target));
            if (!string.IsNullOrWhiteSpace(scope.PreviousBusinessKey)) keys.Add(scope.PreviousBusinessKey);
        }

        foreach (var entry in all.Where(entry => entities.Contains(entry.Group.EntityType)))
            if (scope.Kind == AIImportValidationScopeKind.Group || keys.Contains(BusinessKey(entry)))
                affected.Add(entry.Item.ImportItemId);

        if (entities.Contains(AIImportEntityType.Category))
        {
            var categoryTokens = all.Where(entry => affected.Contains(entry.Item.ImportItemId)
                                                    && entry.Group.EntityType == AIImportEntityType.Category)
                .SelectMany(entry =>
                {
                    var values = Values(entry.Item);
                    return new[] { values.GetValueOrDefault("CategoryCode"), values.GetValueOrDefault("Name") };
                })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(AIImportSchemaRegistry.Key)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var drink in all.Where(entry => entry.Group.EntityType == AIImportEntityType.Drink))
            {
                var category = Values(drink.Item).GetValueOrDefault("Category");
                if (scope.Kind == AIImportValidationScopeKind.Group
                    || categoryTokens.Contains(AIImportSchemaRegistry.Key(category)))
                    affected.Add(drink.Item.ImportItemId);
            }
        }

        return affected;
    }

    private static string BusinessKey((ImportGroup Group, ImportItem Item) entry) =>
        AIImportBusinessKeys.Create(entry.Group.EntityType, Values(entry.Item));

    private static Dictionary<string, string?> Values(ImportItem item) =>
        JsonSerializer.Deserialize<Dictionary<string, string?>>(item.NormalizedDataJson, JsonOptions) ?? new();
}

public sealed class AIImportPreviewValidator(
    AIImportCandidateValidator candidateValidator,
    AIImportResolutionEngine resolutionEngine)
{
    public AIImportCandidateValidationResult ValidateCandidate(
        AIImportEntityType entityType,
        IReadOnlyDictionary<string, string?> values,
        decimal confidence,
        IEnumerable<AIImportErrorDto> sourceIssues,
        bool manualReviewConfirmed,
        string currentStatus,
        string action,
        string? aiErrorCode = null) =>
        candidateValidator.Validate(entityType, values, confidence, sourceIssues,
            manualReviewConfirmed, currentStatus, action, aiErrorCode);

    public HashSet<int> ResolveScope(
        IReadOnlyList<(ImportGroup Group, ImportItem Item)> all,
        AIImportValidationScope scope) => resolutionEngine.ResolveScope(all, scope);
}
