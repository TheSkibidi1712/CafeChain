using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CafeChain.Application.DTOs.AIImport;
using CafeChain.Models.AIImport;

namespace CafeChain.Application.Services.AIImport;

public static class AIImportIssueSeverities
{
    public const string Error = "ERROR";
    public const string Review = "REVIEW";
    public const string Warning = "WARNING";
}

public static class AIImportIssueResolutions
{
    public const string EditField = "EDIT_FIELD";
    public const string RemapGroup = "REMAP_GROUP";
    public const string Acknowledge = "ACKNOWLEDGE";
    public const string ManualReview = "MANUAL_REVIEW";
    public const string SkipConflict = "SKIP_CONFLICT";
    public const string ReuploadOrSkip = "REUPLOAD_OR_SKIP";
}

public static class AIImportColumnClassifications
{
    public const string Mapped = "MAPPED";
    public const string Ignored = "IGNORED";
    public const string Unknown = "UNKNOWN";
    public const string Forbidden = "FORBIDDEN";
}

public sealed class AIImportSourceColumn
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Classification { get; set; } = AIImportColumnClassifications.Unknown;
    public string? TargetField { get; set; }
    public AIImportSourceLocator SourceLocator { get; init; } = new();
    public string? Reason { get; set; }
}

public static class AIImportSourceColumnBuilder
{
    public static List<(int Index, string Key, string Label)> Build(IReadOnlyList<string?> labels, int firstColumn = 1)
    {
        var normalized = labels.Select(label => string.IsNullOrWhiteSpace(label) ? string.Empty : label.Trim()).ToList();
        var counts = normalized.Where(label => label.Length > 0)
            .GroupBy(label => label, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var result = new List<(int, string, string)>();
        for (var index = 0; index < normalized.Count; index++)
        {
            var label = normalized[index];
            if (label.Length == 0) continue;
            var columnName = AIImportRegionData.ColumnName(firstColumn + index);
            var key = counts[label] > 1 ? $"{label} [{columnName}]" : label;
            result.Add((index, key, label));
        }
        return result;
    }

    public static Dictionary<string, string?> RebindMapping(
        IReadOnlyDictionary<string, string?> mapping,
        IReadOnlyList<(int Index, string Key, string Label)> columns) =>
        mapping.ToDictionary(pair => pair.Key, pair =>
        {
            if (string.IsNullOrWhiteSpace(pair.Value)) return null;
            var matches = columns.Where(column => string.Equals(column.Label, pair.Value, StringComparison.OrdinalIgnoreCase)).ToList();
            return matches.Count == 1 ? matches[0].Key : null;
        }, StringComparer.OrdinalIgnoreCase);
}

public static class AIImportValidationContract
{
    public static AIImportErrorDto Issue(
        string code,
        string message,
        string severity,
        string? field = null,
        AIImportPositionDto? locator = null,
        string? resolution = null,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        var values = metadata == null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(metadata, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(resolution)) values["resolution"] = resolution;
        return new AIImportErrorDto
        {
            Code = code,
            Message = message,
            Field = field,
            Severity = severity,
            Position = locator,
            SourceLocator = locator,
            Metadata = values
        };
    }

    public static string ResolveStatus(
        string currentStatus,
        string action,
        IEnumerable<AIImportErrorDto> issues,
        bool manualReviewConfirmed)
    {
        if (action == AIImportActions.Skip)
            return AIImportItemStatuses.Skipped;
        if (currentStatus == AIImportItemStatuses.Imported)
            return AIImportItemStatuses.Imported;

        var effective = issues.Where(issue => !IsResolvedManualReview(issue, manualReviewConfirmed)).ToList();
        if (effective.Any(issue => issue.Severity == AIImportIssueSeverities.Error)) return AIImportItemStatuses.Error;
        if (effective.Any(issue => issue.Severity == AIImportIssueSeverities.Review)) return AIImportItemStatuses.ReviewRequired;
        if (effective.Any(issue => issue.Severity == AIImportIssueSeverities.Warning)) return AIImportItemStatuses.Warning;
        return AIImportItemStatuses.Valid;
    }

    public static bool IsResolvedManualReview(AIImportErrorDto issue, bool confirmed) =>
        confirmed
        && issue.Severity == AIImportIssueSeverities.Review
        && issue.Metadata.TryGetValue("resolution", out var resolution)
        && string.Equals(Convert.ToString(resolution), AIImportIssueResolutions.ManualReview, StringComparison.Ordinal);

    public static string PayloadHash(string normalizedDataJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalJson(normalizedDataJson))));

    private static string CanonicalJson(string value)
    {
        try
        {
            var dictionary = JsonSerializer.Deserialize<Dictionary<string, string?>>(value) ?? new();
            return JsonSerializer.Serialize(dictionary.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return value;
        }
    }
}

public static class AIImportBusinessKeys
{
    private static readonly AIImportEntityRegistry Registry = new();

    public static string Create(AIImportEntityType entity, IReadOnlyDictionary<string, string?> values) =>
        Registry.BusinessKey(entity, values);
}

public static class AIImportReferenceStatuses
{
    public const string Found = "FOUND";
    public const string NotFound = "NOT_FOUND";
    public const string Ambiguous = "AMBIGUOUS";
    public const string Inactive = "INACTIVE";
    public const string PendingInSession = "PENDING_IN_SESSION";
    public const string Forbidden = "FORBIDDEN";
}

public sealed record AIImportReferenceResult<T>(string Status, T? Value, int MatchCount = 0)
{
    public bool IsResolved => Status is AIImportReferenceStatuses.Found or AIImportReferenceStatuses.PendingInSession;
}

public static class AIImportReferenceResolver
{
    public static AIImportReferenceResult<T> Resolve<T>(
        string? input,
        IEnumerable<T> active,
        IEnumerable<T> inactive,
        Func<T, string?> code,
        Func<T, string?> name,
        Func<T, bool>? pending = null)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new(AIImportReferenceStatuses.NotFound, default);
        var matches = active.Where(item => Same(code(item), input) || Same(name(item), input)).Distinct().ToList();
        if (matches.Count > 1) return new(AIImportReferenceStatuses.Ambiguous, default, matches.Count);
        if (matches.Count == 1)
            return new(pending?.Invoke(matches[0]) == true
                ? AIImportReferenceStatuses.PendingInSession
                : AIImportReferenceStatuses.Found, matches[0], 1);
        var inactiveMatches = inactive.Where(item => Same(code(item), input) || Same(name(item), input)).Distinct().Count();
        return inactiveMatches > 0
            ? new(AIImportReferenceStatuses.Inactive, default, inactiveMatches)
            : new(AIImportReferenceStatuses.NotFound, default);
    }

    private static bool Same(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right)
        && AIImportSchemaRegistry.Key(left) == AIImportSchemaRegistry.Key(right);
}
