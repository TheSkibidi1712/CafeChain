using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Infrastructure.Configurations;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.AI;

public sealed class PexelsMetadataScorer : IPexelsMetadataScorer
{
    private readonly AIImagePipelineOptions _options;

    public PexelsMetadataScorer(IOptions<AIImagePipelineOptions> options) => _options = options.Value;

    public PexelsCandidateScoreResult Score(
        PexelsPhotoDTO photo,
        VisualSpecificationDTO specification,
        string matchedQuery)
    {
        var warnings = new List<string>();
        if (photo.Width < _options.MinimumImageWidth || photo.Height < _options.MinimumImageHeight)
            return new(true, 0, ["Ảnh có độ phân giải quá thấp."]);
        if (!MatchesOrientation(photo.Width, photo.Height, specification.Orientation))
            return new(true, 0, ["Ảnh không đúng hướng yêu cầu."]);

        var alt = (photo.Alt ?? string.Empty).ToLowerInvariant();
        var forbidden = specification.ForbiddenKeywords
            .Where(x => ContainsTerm(alt, x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (forbidden.Count > 0)
            return new(true, 0, [$"Metadata chứa đối tượng loại trừ: {string.Join(", ", forbidden)}."]);

        var required = specification.RequiredKeywords.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var requiredRatio = required.Count == 0 ? 0.5 : required.Count(x => ContainsTerm(alt, x)) / (double)required.Count;
        var primaryTerms = specification.PrimarySubject.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length > 3).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var primaryRatio = primaryTerms.Count == 0 ? 0.5 : primaryTerms.Count(x => ContainsTerm(alt, x)) / (double)primaryTerms.Count;
        var subjectTerms = specification.SubjectType.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var subjectMatch = subjectTerms.Any(x => ContainsTerm(alt, x)) || SubjectSynonymMatch(alt, specification.SubjectType);
        var colorMatch = specification.DominantColors.Any(x => ContainsTerm(alt, x)) || !string.IsNullOrWhiteSpace(photo.AverageColor);
        var quality = Math.Clamp(Math.Min(photo.Width, photo.Height) / 1600d, 0, 1);
        var specificity = Math.Clamp(matchedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length / 10d, 0.4, 1);

        var score = 0.35 * primaryRatio
            + 0.20 * requiredRatio
            + 0.15 * (subjectMatch ? 1 : 0.35)
            + 0.10 * (colorMatch ? 1 : 0.4)
            + 0.10
            + 0.10 * quality;
        score = Math.Clamp(score * (0.9 + 0.1 * specificity), 0, 1);
        if (requiredRatio < 0.5) warnings.Add("Alt text khớp ít từ khóa bắt buộc; cần người dùng kiểm tra ảnh.");
        if (!subjectMatch) warnings.Add("Metadata chưa xác nhận rõ loại đối tượng.");
        warnings.Add("Điểm chỉ dựa trên metadata; cần người dùng xác nhận nội dung ảnh.");
        return new(false, Math.Round(score, 4), warnings);
    }

    private static bool MatchesOrientation(int width, int height, string orientation)
    {
        if (width <= 0 || height <= 0) return false;
        var ratio = width / (double)height;
        return orientation.ToLowerInvariant() switch
        {
            "square" => ratio is >= 0.80 and <= 1.25,
            "portrait" => ratio < 0.90,
            "landscape" => ratio > 1.10,
            _ => true
        };
    }

    private static bool ContainsTerm(string text, string term) =>
        !string.IsNullOrWhiteSpace(term) && text.Contains(term.Trim().ToLowerInvariant(), StringComparison.Ordinal);

    private static bool SubjectSynonymMatch(string alt, string subjectType) => subjectType.ToLowerInvariant() switch
    {
        "beverage" => new[] { "drink", "tea", "coffee", "latte", "juice", "glass", "cup" }.Any(x => alt.Contains(x)),
        "food ingredient" => new[] { "ingredient", "topping", "pearl", "jelly", "pudding", "bowl", "food" }.Any(x => alt.Contains(x)),
        _ => false
    };
}
