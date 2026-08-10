using System.Globalization;
using System.Text.RegularExpressions;
using CafeChain.Application.DTOs.AI;

namespace CafeChain.Application.Services.AI;

public static partial class SupplierIntelligencePresentation
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");

    public static string Confidence(string? value) => value switch
    {
        "HIGH" => "Cao",
        "MEDIUM" => "Vừa phải",
        "INSUFFICIENT_DATA" => "Chưa đủ dữ liệu",
        _ => "Chưa xác định"
    };

    public static string Component(string value) => value switch
    {
        "price" => "giá mua",
        "onTime" => "giao đúng hẹn",
        "fill" => "đáp ứng đủ số lượng",
        "quality" => "chất lượng hàng",
        "leadTime" => "thời gian giao",
        _ => "chỉ số chưa xác định"
    };

    public static string BuildFallbackExplanation(SupplierExplanationContextDto context)
    {
        var components = context.ComponentScores
            .Select(item => $"{Component(item.Key)} {FormatScore(item.Value)} điểm")
            .ToArray();
        var warnings = context.Warnings
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToArray();

        var explanation =
            $"Nhà cung cấp đạt {FormatScore(context.TotalScore)}/100 điểm. " +
            $"Mức độ tin cậy: {Confidence(context.Confidence).ToLower(VietnameseCulture)}. " +
            $"Điểm thành phần gồm {string.Join(", ", components)}.";

        return warnings.Length == 0
            ? explanation
            : $"{explanation} Lưu ý: {string.Join(" ", warnings)}";
    }

    public static bool ContainsTechnicalTerms(string value) =>
        !string.IsNullOrWhiteSpace(value) && TechnicalTermRegex().IsMatch(value);

    private static string FormatScore(decimal value) =>
        value.ToString("0.#", VietnameseCulture);

    [GeneratedRegex(
        @"(?<![\p{L}\p{N}_])(?:ranking|metric|unknown|fallback|confidence|shadowmode|pilot|backend|high|medium|insufficient_data|packaged|loose|confirmed)(?![\p{L}\p{N}_])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TechnicalTermRegex();
}
