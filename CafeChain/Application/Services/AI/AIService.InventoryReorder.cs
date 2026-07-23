using System.Text.Json;
using System.Text.Json.Serialization;
using CafeChain.Application.DTOs.AI;

namespace CafeChain.Application.Services.AI;

public sealed partial class AIService
{
    private const string InventoryReorderSkill = "inventory-reorder-explanation";

    public async Task<InventoryReorderExplanationResultDto> ExplainInventoryReorderAsync(
        InventoryReorderExplanationContextDto context,
        CancellationToken cancellationToken = default)
    {
        var fallback = BuildReorderFallback(context);
        if (!_options.Enabled || !string.Equals(_options.Provider, "Ollama", StringComparison.OrdinalIgnoreCase))
            return Fallback(fallback, "Ollama đang tắt; sử dụng giải thích từ rule.");

        try
        {
            var skill = await _skillCatalog.GetNamedSkillAsync(InventoryReorderSkill, cancellationToken);
            var prompt = $"{skill.Content}\n\nJSON Schema bắt buộc:\n{skill.JsonSchema}";
            var payload = JsonSerializer.Serialize(context, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var response = await _ollama.ChatAsync(prompt, payload, InventoryReorderSkill, cancellationToken);
            if (!response.Success || string.IsNullOrWhiteSpace(response.Content))
                return Fallback(fallback, "Ollama không khả dụng; sử dụng giải thích từ rule.");

            var parsed = JsonSerializer.Deserialize<InventoryReorderAiResponseDto>(
                StripMarkdownFence(response.Content),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = false,
                    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
                });
            if (parsed == null || !EchoMatches(context, parsed))
                return Fallback(fallback, "Phản hồi AI không khớp dữ liệu rule và đã bị từ chối.");

            var explanation = parsed.Explanation.Trim();
            if (explanation.Length is < 1 or > 600)
                return Fallback(fallback, "Giải thích AI không đạt giới hạn nội dung và đã bị từ chối.");

            return new InventoryReorderExplanationResultDto
            {
                Success = true,
                Explanation = explanation,
                UsedOllama = true,
                UsedFallback = false,
                Warnings = skill.Warnings.ToList()
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fallback(fallback, "AI phản hồi quá thời gian; sử dụng giải thích từ rule.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            _logger.LogWarning("Inventory reorder explanation rejected. Skill={Skill} ErrorType={ErrorType}",
                InventoryReorderSkill, ex.GetType().Name);
            return Fallback(fallback, "Phản hồi AI không hợp lệ; sử dụng giải thích từ rule.");
        }
    }

    private static bool EchoMatches(
        InventoryReorderExplanationContextDto source,
        InventoryReorderAiResponseDto response) =>
        source.IngredientId == response.IngredientId
        && string.Equals(source.RecommendationLevel, response.RecommendationLevel, StringComparison.Ordinal)
        && source.UsableStock == response.UsableStock
        && source.MinimumStock == response.MinimumStock
        && source.PendingIncoming == response.PendingIncoming
        && source.SuggestedQuantity == response.SuggestedQuantity;

    private static string BuildReorderFallback(InventoryReorderExplanationContextDto context) =>
        string.IsNullOrWhiteSpace(context.DeterministicReason)
            ? $"{context.IngredientName}: tồn khả dụng {context.UsableStock:N3} {context.Unit}, "
              + $"đang về {context.PendingIncoming:N3} {context.Unit}, đề xuất {context.SuggestedQuantity:N3} {context.Unit}."
            : context.DeterministicReason.Trim();

    private static InventoryReorderExplanationResultDto Fallback(string explanation, string warning) => new()
    {
        Success = true,
        Explanation = explanation,
        UsedFallback = true,
        Warnings = [warning]
    };
}
