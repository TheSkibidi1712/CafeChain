using System.Text.Json;
using System.Text.Json.Serialization;
using CafeChain.Application.DTOs.AI;

namespace CafeChain.Application.Services.AI;

public sealed partial class AIService
{
    private static readonly JsonSerializerOptions IntelligenceExplanationJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    public Task<TypedExplanationResultDto> ExplainForecastAsync(ForecastExplanationContextDto context, CancellationToken ct = default) =>
        ExplainTyped("forecast-result-explanation", context,
            x => x.GetProperty("runId").GetInt64() == context.RunId
                && x.GetProperty("modelType").GetString() == context.ModelType
                && x.GetProperty("pointForecast").GetDecimal() == context.PointForecast
                && x.GetProperty("lowerBound").GetDecimal() == context.LowerBound
                && x.GetProperty("upperBound").GetDecimal() == context.UpperBound,
            $"Dự báo {context.PointForecast:N0}, khoảng tham chiếu {context.LowerBound:N0}–{context.UpperBound:N0}; mô hình {context.ModelType}, WAPE {context.Wape:P1}. {string.Join(" ", context.Warnings)}", ct);

    public Task<TypedExplanationResultDto> ExplainSupplierScoreAsync(SupplierExplanationContextDto context, CancellationToken ct = default) =>
        ExplainTyped("supplier-score-explanation", context,
            x => x.GetProperty("supplierId").GetInt32() == context.SupplierId
                && x.GetProperty("totalScore").GetDecimal() == context.TotalScore
                && ComponentsMatch(x.GetProperty("componentScores"), context.ComponentScores),
            SupplierIntelligencePresentation.BuildFallbackExplanation(context), ct);

    public Task<TypedExplanationResultDto> ExplainAnomalyAsync(AnomalyExplanationContextDto context, CancellationToken ct = default) =>
        ExplainTyped("anomaly-explanation", context,
            x => x.GetProperty("anomalyId").GetInt64() == context.AnomalyId
                && x.GetProperty("metricCode").GetString() == context.MetricCode
                && x.GetProperty("currentValue").GetDecimal() == context.CurrentValue
                && x.GetProperty("baselineValue").GetDecimal() == context.BaselineValue,
            OperationalAnomalyPresentation.BuildFallbackExplanation(
                context.MetricDisplayName,
                context.CurrentValueDisplay,
                context.BaselineValueDisplay,
                context.DirectionDescription,
                context.SuggestedChecks), ct);

    private async Task<TypedExplanationResultDto> ExplainTyped<T>(string skillName, T context, Func<JsonElement, bool> echoValidator, string fallback, CancellationToken ct)
    {
        if (!_options.Enabled || !string.Equals(_options.Provider, "Ollama", StringComparison.OrdinalIgnoreCase))
            return ExplanationFallback(fallback, "Tính năng giải thích tự động đang tắt.");
        try
        {
            var skill = await _skillCatalog.GetNamedSkillAsync(skillName, ct);
            var response = await _ollama.ChatAsync($"{skill.Content}\n\nJSON Schema bắt buộc:\n{skill.JsonSchema}", JsonSerializer.Serialize(context, IntelligenceExplanationJson), skillName, ct);
            if (!response.Success || string.IsNullOrWhiteSpace(response.Content))
                return ExplanationFallback(fallback, "Dịch vụ giải thích tự động chưa sẵn sàng.");
            using var document = JsonDocument.Parse(StripMarkdownFence(response.Content)); var root = document.RootElement;
            if (!echoValidator(root))
                return ExplanationFallback(fallback, "Phản hồi tự động không khớp dữ liệu nguồn và đã bị từ chối.");
            var explanation = root.GetProperty("explanation").GetString()?.Trim() ?? string.Empty;
            if (explanation.Length is < 1 or > 1000) return ExplanationFallback(fallback, "Giải thích AI vượt giới hạn.");
            if (skillName == "supplier-score-explanation"
                && SupplierIntelligencePresentation.ContainsTechnicalTerms(explanation))
                return ExplanationFallback(fallback, "Giải thích AI còn chứa thuật ngữ kỹ thuật nên đã được thay bằng nội dung tiếng Việt.");
            return new TypedExplanationResultDto { Success = true, Explanation = explanation, UsedOllama = true, Warnings = skill.Warnings };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return ExplanationFallback(fallback, "AI phản hồi quá thời gian."); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentOutOfRangeException)
        { _logger.LogWarning("Typed AI explanation rejected. Skill={Skill} ErrorType={ErrorType}", skillName, ex.GetType().Name); return ExplanationFallback(fallback, "Phản hồi tự động không đúng định dạng yêu cầu."); }
    }

    private static bool ComponentsMatch(JsonElement element, IReadOnlyDictionary<string, decimal> expected)
    {
        if (element.ValueKind != JsonValueKind.Object || element.EnumerateObject().Count() != expected.Count) return false;
        return expected.All(x => element.TryGetProperty(x.Key, out var value) && value.GetDecimal() == x.Value);
    }

    private static TypedExplanationResultDto ExplanationFallback(string text, string warning) => new() { Success = true, Explanation = text.Trim(), UsedFallback = true, Warnings = [warning] };
}
