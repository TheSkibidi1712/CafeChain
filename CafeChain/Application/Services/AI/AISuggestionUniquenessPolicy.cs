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

    public static double NameSimilarity(string? left, string? right)
    {
        var a = NormalizeTextKey(left);
        var b = NormalizeTextKey(right);
        if (a.Length == 0 || b.Length == 0) return 0;
        if (a == b) return 1;
        var distance = LevenshteinDistance(a, b);
        return 1d - distance / (double)Math.Max(a.Length, b.Length);
    }

    public static double TokenJaccard(string? left, string? right)
    {
        var a = Tokenize(left);
        var b = Tokenize(right);
        if (a.Count == 0 || b.Count == 0) return 0;
        var intersection = a.Intersect(b).Count();
        var union = a.Union(b).Count();
        return union == 0 ? 0 : intersection / (double)union;
    }

    public static bool IsNearDuplicate(
        string? name,
        string? description,
        IEnumerable<(string Name, string? Description)> existing,
        double nameThreshold,
        double compositeThreshold,
        out List<string> signals)
    {
        signals = [];
        foreach (var candidate in existing)
        {
            var nameScore = NameSimilarity(name, candidate.Name);
            var descriptionScore = TokenJaccard(description, candidate.Description);
            var composite = 0.75 * nameScore + 0.25 * descriptionScore;
            if (nameScore >= nameThreshold) signals.Add($"near-name:{candidate.Name}:{nameScore:0.00}");
            if (composite >= compositeThreshold) signals.Add($"concept:{candidate.Name}:{composite:0.00}");
            if (nameScore >= nameThreshold || composite >= compositeThreshold) return true;
        }
        return false;
    }

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

    private static HashSet<string> Tokenize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        var normalized = value.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(character) ? char.ToUpperInvariant(character) : ' ');
        }
        return builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }
}
