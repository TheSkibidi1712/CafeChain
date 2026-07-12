using System.Globalization;
using System.Text;

namespace CafeChain.Application.Services.AI;

/// <summary>
/// Shared duplicate policy for AI-created master data. Callers explicitly pass
/// unique business keys; foreign keys and repeatable fields must not be passed.
/// </summary>
public static class AISuggestionUniquenessPolicy
{
    public static string NormalizeTextKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = value.Trim().Replace('đ', 'd').Replace('Đ', 'D')
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character)) builder.Append(char.ToUpperInvariant(character));
        }
        return builder.ToString();
    }

    public static string NormalizeCodeKey(string? value) => NormalizeTextKey(value);

    public static List<T> FilterDistinctSuggestions<T>(
        IEnumerable<T> suggestions,
        Func<T, string?> nameSelector,
        Func<T, string?> codeSelector,
        IEnumerable<string?> existingNames,
        IEnumerable<string?> existingCodes,
        out int rejectedCount)
    {
        var names = new HashSet<string>(existingNames.Select(NormalizeTextKey).Where(x => x.Length > 0));
        var codes = new HashSet<string>(existingCodes.Select(NormalizeCodeKey).Where(x => x.Length > 0));
        var result = new List<T>();
        rejectedCount = 0;
        foreach (var suggestion in suggestions)
        {
            var nameKey = NormalizeTextKey(nameSelector(suggestion));
            var codeKey = NormalizeCodeKey(codeSelector(suggestion));
            if (nameKey.Length == 0 || codeKey.Length == 0 || !names.Add(nameKey) || !codes.Add(codeKey))
            {
                rejectedCount++;
                continue;
            }
            result.Add(suggestion);
        }
        return result;
    }
}
