using System.Text.Json;
using System.Text.Json.Serialization;
using CafeChain.Application.DTOs.Admin.Dashboard;

namespace CafeChain.Application.Services.AI;

public sealed partial class AIService
{
    private const string DashboardIntentSkill = "dashboard-intent-parser";
    private const string DashboardExplanationSkill = "dashboard-insight-explanation";

    private static readonly JsonSerializerOptions DashboardJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<DashboardIntentParseResultDto> ParseDashboardIntentAsync(
        DashboardPromptRequestDto request,
        IReadOnlyList<string> allowedStoreNames,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !string.Equals(_options.Provider, "Ollama", StringComparison.OrdinalIgnoreCase))
            return Unsupported("Ollama đang tắt; không thể diễn giải câu hỏi này.");

        try
        {
            var skill = await _skillCatalog.GetNamedSkillAsync(DashboardIntentSkill, cancellationToken);
            var payload = JsonSerializer.Serialize(new
            {
                request.Prompt,
                request.Locale,
                today = DateTime.Today.ToString("yyyy-MM-dd"),
                allowedStoreNames
            }, DashboardJsonOptions);
            var response = await _ollama.ChatAsync(
                $"{skill.Content}\n\nJSON Schema bắt buộc:\n{skill.JsonSchema}", payload,
                DashboardIntentSkill, cancellationToken);
            if (!response.Success || string.IsNullOrWhiteSpace(response.Content))
                return Unsupported("AI không khả dụng; câu hỏi không khớp parser deterministic.");

            var intent = JsonSerializer.Deserialize<DashboardIntentDto>(
                StripMarkdownFence(response.Content), DashboardJsonOptions);
            if (intent == null) return Unsupported("AI không trả về intent hợp lệ.");
            return new DashboardIntentParseResultDto
            {
                Success = true,
                Message = "Đã phân tích câu hỏi. Vui lòng kiểm tra intent trước khi chạy.",
                Intent = intent,
                UsedOllama = true,
                Warnings = skill.Warnings.ToList()
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unsupported("AI phản hồi quá thời gian.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            _logger.LogWarning("Dashboard intent rejected. ErrorType={ErrorType}", ex.GetType().Name);
            return Unsupported("Phản hồi AI không đúng contract và đã bị từ chối.");
        }
    }

    public async Task<DashboardExplanationResultDto> ExplainDashboardInsightAsync(
        DashboardInsightExplanationContextDto context,
        CancellationToken cancellationToken = default)
    {
        var fallback = BuildDashboardFallback(context);
        if (!_options.Enabled || !string.Equals(_options.Provider, "Ollama", StringComparison.OrdinalIgnoreCase))
            return DashboardFallback(fallback, "Ollama đang tắt; sử dụng giải thích từ rule.");
        try
        {
            var skill = await _skillCatalog.GetNamedSkillAsync(DashboardExplanationSkill, cancellationToken);
            var response = await _ollama.ChatAsync(
                $"{skill.Content}\n\nJSON Schema bắt buộc:\n{skill.JsonSchema}",
                JsonSerializer.Serialize(context, DashboardJsonOptions), DashboardExplanationSkill, cancellationToken);
            if (!response.Success || string.IsNullOrWhiteSpace(response.Content))
                return DashboardFallback(fallback, "Ollama không khả dụng; sử dụng giải thích từ rule.");
            var parsed = JsonSerializer.Deserialize<DashboardExplanationAiDto>(
                StripMarkdownFence(response.Content), DashboardJsonOptions);
            if (parsed == null || parsed.AnalysisId != context.AnalysisId || parsed.Widget != context.Widget)
                return DashboardFallback(fallback, "Phản hồi AI không khớp dữ liệu phân tích và đã bị từ chối.");
            var summary = parsed.Summary.Trim();
            if (summary.Length is < 1 or > 1200)
                return DashboardFallback(fallback, "Giải thích AI vượt giới hạn nội dung.");
            return new DashboardExplanationResultDto
            {
                Success = true,
                Explanation = summary,
                Summary = summary,
                Inferences = parsed.Inferences,
                Recommendations = parsed.Recommendations,
                UsedOllama = true,
                Warnings = skill.Warnings.ToList()
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DashboardFallback(fallback, "AI phản hồi quá thời gian.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            _logger.LogWarning("Dashboard explanation rejected. ErrorType={ErrorType}", ex.GetType().Name);
            return DashboardFallback(fallback, "Phản hồi AI không hợp lệ; sử dụng giải thích từ rule.");
        }
    }

    private static DashboardIntentParseResultDto Unsupported(string message) => new()
    {
        Success = false, ErrorCode = "UNSUPPORTED_INTENT", Message = message, UsedFallback = true
    };

    private static string BuildDashboardFallback(DashboardInsightExplanationContextDto context)
    {
        if (context.Insights.Count > 0) return string.Join(" ", context.Insights.Select(x => x.Message));
        var comparison = context.Comparison;
        return comparison.BaselineValue.HasValue
            ? $"Giá trị hiện tại là {comparison.CurrentValue:N0}, so với kỳ trước {comparison.BaselineValue:N0}."
            : $"Giá trị trong kỳ là {comparison.CurrentValue:N0}.";
    }

    private static DashboardExplanationResultDto DashboardFallback(string text, string warning) => new()
    {
        Success = true,
        Explanation = text,
        Summary = text,
        UsedFallback = true,
        Warnings = [warning]
    };

    private sealed class DashboardExplanationAiDto
    {
        public Guid AnalysisId { get; set; }
        public DashboardAnalyticsWidget Widget { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<DashboardNarrativeItemDto> Inferences { get; set; } = [];
        public List<DashboardNarrativeItemDto> Recommendations { get; set; } = [];
    }
}
