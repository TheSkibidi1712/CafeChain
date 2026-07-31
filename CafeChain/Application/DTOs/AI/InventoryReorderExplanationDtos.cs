using System.Text.Json.Serialization;

namespace CafeChain.Application.DTOs.AI;

/// <summary>
/// Deterministic facts used to explain one reorder decision.
///
/// This type deliberately contains facts only.  It is not a command and it
/// must never be used as a source of quantities for a procurement mutation.
/// Nullable numeric fields are intentional: a missing fact is different from
/// the value zero and is reported by the deterministic rule as
/// <c>DATA_INCOMPLETE</c>.
/// </summary>
public sealed class InventoryReorderExplanationContextDto
{
    public int StoreId { get; init; }
    public string StoreName { get; init; } = string.Empty;
    public int IngredientId { get; init; }
    public string IngredientCode { get; init; } = string.Empty;
    public string IngredientName { get; init; } = string.Empty;
    public string BaseUnitCode { get; init; } = string.Empty;

    public DateTime? AnalysisFromUtc { get; init; }
    public DateTime? AnalysisToUtc { get; init; }
    public DateTime? CalculatedAtUtc { get; init; }
    public string CalculationVersion { get; init; } = string.Empty;

    public decimal? OnHandQuantity { get; init; }
    public decimal? ReservedQuantity { get; init; }
    public decimal? AvailableStock { get; init; }
    public decimal? MinimumStock { get; init; }
    public decimal? AverageDailyConsumption { get; init; }
    public int? LeadTimeDays { get; init; }
    public decimal? ReorderPoint { get; init; }
    public decimal? IncomingQuantity { get; init; }
    public decimal? ProjectedStock { get; init; }
    public decimal? RawDemand { get; init; }
    public decimal? ProcurementCoveredQuantity { get; init; }
    public decimal? RemainingDemand { get; init; }

    public decimal? PackageBaseQuantity { get; init; }
    public decimal? SuggestedPackageCount { get; init; }
    public decimal? FinalSuggestedQuantity { get; init; }
    public decimal? MinimumOrderPackageCount { get; init; }
    public decimal? PackagePrice { get; init; }
    public DateTime? PriceEffectiveAt { get; init; }

    [JsonIgnore]
    public DateTime? PriceEffectiveAtUtc { get; init; }

    public decimal? EstimatedCost { get; init; }

    public int? IngredientSupplierId { get; init; }
    public int? SupplierId { get; init; }
    public string SupplierCode { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public string SuggestionStatus { get; init; } = string.Empty;
    public IReadOnlyList<string> ReasonCodes { get; init; } = [];
    public string DeterministicReason { get; init; } = string.Empty;
    public bool CanConfirm { get; init; }
    public int? ActiveRestockRequestId { get; init; }

    // Compatibility aliases for callers being migrated from the first
    // implementation.  They are intentionally excluded from the AI payload;
    // Normalize() in AIService.InventoryReorder resolves them when present.
    [JsonIgnore]
    public string RecommendationLevel { get; init; } = string.Empty;

    [JsonIgnore]
    public decimal UsableStock { get; init; }

    [JsonIgnore]
    public decimal PendingIncoming { get; init; }

    [JsonIgnore]
    public decimal SuggestedQuantity { get; init; }

    [JsonIgnore]
    public string Unit { get; init; } = string.Empty;

    [JsonIgnore]
    public decimal? AvailableQuantity { get; init; }

    [JsonIgnore]
    public decimal? EstimatedAmount { get; init; }
}

/// <summary>
/// Service result.  The four text fields are the complete raw AI contract;
/// operational flags and warnings are added by the application boundary and
/// are never sent to Ollama or accepted from it.
/// </summary>
public sealed class InventoryReorderExplanationResultDto
{
    public bool Success { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string Explanation { get; init; } = string.Empty;
    public string Risk { get; init; } = string.Empty;
    public string RecommendedActionText { get; init; } = string.Empty;
    public bool UsedOllama { get; init; }
    public bool UsedFallback { get; init; }
    public List<string> Warnings { get; init; } = [];
}

/// <summary>
/// Strict internal representation of the model response.  Keep this type
/// limited to the four allowed properties so JsonUnmappedMemberHandling can
/// reject accidental business fields, commands, or echoed quantities.
/// </summary>
internal sealed class InventoryReorderAiResponseDto
{
    [JsonPropertyName("Summary")]
    public string Summary { get; init; } = string.Empty;

    [JsonPropertyName("Explanation")]
    public string Explanation { get; init; } = string.Empty;

    [JsonPropertyName("Risk")]
    public string Risk { get; init; } = string.Empty;

    [JsonPropertyName("RecommendedActionText")]
    public string RecommendedActionText { get; init; } = string.Empty;
}
