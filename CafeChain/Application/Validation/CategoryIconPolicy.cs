using System.Globalization;
using System.Text;

namespace CafeChain.Application.Validation;

public static class CategoryIconPolicy
{
    public const int MaximumLength = 10;

    public static bool TryNormalize(
        string? value,
        out string? normalized,
        out string? errorMessage)
    {
        normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        errorMessage = null;

        if (normalized == null)
        {
            return true;
        }

        if (normalized.Length > MaximumLength)
        {
            errorMessage = $"Icon tối đa {MaximumLength} ký tự.";
            return false;
        }

        if (normalized.IndexOfAny(['<', '>', '&']) >= 0)
        {
            errorMessage = "Icon không được chứa HTML.";
            return false;
        }

        var textElements = StringInfo.GetTextElementEnumerator(normalized);
        var textElementCount = 0;
        while (textElements.MoveNext())
        {
            textElementCount++;
            if (textElementCount > 1)
            {
                errorMessage = "Chỉ được chọn một biểu tượng Unicode.";
                return false;
            }
        }

        var containsSymbol = false;
        foreach (var rune in normalized.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            switch (category)
            {
                case UnicodeCategory.MathSymbol:
                case UnicodeCategory.CurrencySymbol:
                case UnicodeCategory.ModifierSymbol:
                case UnicodeCategory.OtherSymbol:
                    containsSymbol = true;
                    break;
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                case UnicodeCategory.EnclosingMark:
                case UnicodeCategory.Format:
                    break;
                default:
                    errorMessage = "Icon phải là một biểu tượng Unicode, không phải chữ hoặc số.";
                    return false;
            }
        }

        if (!containsSymbol)
        {
            errorMessage = "Icon phải là một biểu tượng Unicode hợp lệ.";
            return false;
        }

        return true;
    }

    public static string? NormalizeOrThrow(string? value)
    {
        if (TryNormalize(value, out var normalized, out var errorMessage))
        {
            return normalized;
        }

        throw new ArgumentException(errorMessage);
    }
}
