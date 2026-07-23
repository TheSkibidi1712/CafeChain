using System.Text.Json.Serialization;

namespace CafeChain.Application.DTOs.AI;

public sealed class InventoryReorderExplanationContextDto
{
    public int IngredientId { get; init; }
    public string IngredientName { get; init; } = string.Empty;
    public string RecommendationLevel { get; init; } = string.Empty;
    public decimal UsableStock { get; init; }
    public decimal MinimumStock { get; init; }
    public decimal PendingIncoming { get; init; }
    public decimal SuggestedQuantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public string DeterministicReason { get; init; } = string.Empty;
}

public sealed class InventoryReorderExplanationResultDto
{
    public bool Success { get; init; }
    public string Explanation { get; init; } = string.Empty;
    public bool UsedOllama { get; init; }
    public bool UsedFallback { get; init; }
    public List<string> Warnings { get; init; } = [];
}

internal sealed class InventoryReorderAiResponseDto
{
    [JsonPropertyName("ingredientId")]
    public int IngredientId { get; init; }

    [JsonPropertyName("recommendationLevel")]
    public string RecommendationLevel { get; init; } = string.Empty;

    [JsonPropertyName("usableStock")]
    public decimal UsableStock { get; init; }

    [JsonPropertyName("minimumStock")]
    public decimal MinimumStock { get; init; }

    [JsonPropertyName("pendingIncoming")]
    public decimal PendingIncoming { get; init; }

    [JsonPropertyName("suggestedQuantity")]
    public decimal SuggestedQuantity { get; init; }

    [JsonPropertyName("explanation")]
    public string Explanation { get; init; } = string.Empty;
}
