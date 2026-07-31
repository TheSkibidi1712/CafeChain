namespace CafeChain.Application.DTOs.Admin.Procurement;

/// <summary>
/// A deterministic reorder calculation for one store and one analysis window.
/// The dates and calculation version are part of the contract so callers can
/// detect a stale suggestion instead of silently applying an old number.
/// </summary>
public sealed class ReorderSuggestionListDto
{
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public int AnalysisWindowDays { get; set; }
    public DateTime AnalysisFromUtc { get; set; }
    public DateTime AnalysisToUtc { get; set; }
    public DateTime CalculatedAtUtc { get; set; }
    public string CalculationVersion { get; set; } = string.Empty;
    public List<ReorderSuggestionItemDto> Items { get; set; } = new();
}

/// <summary>
/// Canonical rule output.  The old property names at the bottom are retained
/// as compatibility aliases while MVC/notification consumers migrate.  New
/// code must use the canonical names above.
/// </summary>
public sealed class ReorderSuggestionItemDto
{
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public int IngredientId { get; set; }
    public string IngredientCode { get; set; } = string.Empty;
    public string IngredientName { get; set; } = string.Empty;
    public int BaseUnitId { get; set; }
    public string BaseUnitCode { get; set; } = string.Empty;

    // Stock and deterministic demand inputs.
    public decimal OnHandQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal AvailableStock { get; set; }
    public decimal? MinimumStock { get; set; }
    public decimal? AverageDailyConsumption { get; set; }
    public int? LeadTimeDays { get; set; }
    public decimal? ReorderPoint { get; set; }
    public decimal IncomingQuantity { get; set; }
    public decimal? ProjectedStock { get; set; }
    public decimal? RawDemand { get; set; }
    public decimal ProcurementCoveredQuantity { get; set; }
    public decimal? RemainingDemand { get; set; }

    // Selected offer and package rounding.
    public int? IngredientSupplierId { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierCode { get; set; }
    public string? SupplierName { get; set; }
    public int? PackageUnitId { get; set; }
    public decimal? PackageBaseQuantity { get; set; }
    public decimal? PackagePrice { get; set; }
    public DateTime? PriceEffectiveAtUtc { get; set; }
    public int? MinimumOrderPackageCount { get; set; }
    public decimal? SuggestedPackageCount { get; set; }
    public decimal? FinalSuggestedQuantity { get; set; }
    public decimal? EstimatedCost { get; set; }

    // State and audit metadata.
    public string SuggestionStatus { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public List<string> ReasonCodes { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
    public bool CanConfirm { get; set; }
    public bool IsConfirmable
    {
        get => CanConfirm;
        set => CanConfirm = value;
    }
    public int? ActiveRestockRequestId { get; set; }
    public int AnalysisWindowDays { get; set; }
    public DateTime AnalysisFromUtc { get; set; }
    public DateTime AnalysisToUtc { get; set; }
    public DateTime CalculatedAtUtc { get; set; }
    public string CalculationVersion { get; set; } = string.Empty;
    public string? SuggestionToken { get; set; }
    public string MeaningfulSuggestionVersion { get; set; } = string.Empty;

    // Legacy aliases.  They intentionally map to canonical values; this
    // prevents old consumers from accidentally using a second calculation.
    [Obsolete("Use OnHandQuantity.")]
    public decimal AvailableQuantity
    {
        get => OnHandQuantity;
        set => OnHandQuantity = value;
    }

    [Obsolete("Use AvailableStock.")]
    public decimal UsableQuantity
    {
        get => AvailableStock;
        set => AvailableStock = value;
    }

    [Obsolete("Use ProjectedStock.")]
    public decimal ProjectedQuantity
    {
        get => ProjectedStock ?? 0m;
        set => ProjectedStock = value;
    }

    [Obsolete("Use MinimumStock.")]
    public decimal? MinLevel
    {
        get => MinimumStock;
        set => MinimumStock = value;
    }

    [Obsolete("Use AverageDailyConsumption.")]
    public decimal? AverageDailyUsage
    {
        get => AverageDailyConsumption;
        set => AverageDailyConsumption = value;
    }

    [Obsolete("Use IncomingQuantity.")]
    public decimal IncomingApprovedPoQuantity
    {
        get => IncomingQuantity;
        set => IncomingQuantity = value;
    }

    /// <summary>
    /// Legacy field retained for old cards.  It now reports pipeline coverage,
    /// not a second PA-only calculation.
    /// </summary>
    [Obsolete("Use ProcurementCoveredQuantity.")]
    public decimal PendingPurchaseAdviceQuantity
    {
        get => ProcurementCoveredQuantity;
        set => ProcurementCoveredQuantity = value;
    }

    [Obsolete("Use RawDemand or FinalSuggestedQuantity explicitly.")]
    public decimal? SuggestedBaseQuantity
    {
        get => RawDemand;
        set => RawDemand = value;
    }

    [Obsolete("Use EstimatedCost.")]
    public decimal? EstimatedAmount
    {
        get => EstimatedCost;
        set => EstimatedCost = value;
    }

    [Obsolete("Use SuggestionStatus.")]
    public string Status
    {
        get => SuggestionStatus switch
        {
            "URGENT" or "NEAR_REORDER" => "READY",
            "DATA_INCOMPLETE" when ReasonCodes.Contains("MISSING_THRESHOLD") =>
                "MISSING_THRESHOLD",
            "DATA_INCOMPLETE" when ReasonCodes.Contains("INSUFFICIENT_HISTORY") =>
                "INSUFFICIENT_HISTORY",
            "DATA_INCOMPLETE" when ReasonCodes.Contains("NO_ACTIVE_SUPPLIER") =>
                "NO_ACTIVE_SUPPLIER",
            "DATA_INCOMPLETE" when ReasonCodes.Contains("INVALID_CONVERSION") =>
                "INVALID_CONVERSION",
            "DATA_INCOMPLETE" when ReasonCodes.Contains("MISSING_LEAD_TIME") =>
                "MISSING_LEAD_TIME",
            "DATA_INCOMPLETE" when ReasonCodes.Contains("MISSING_COST") =>
                "MISSING_COST",
            _ => SuggestionStatus
        };
        set => SuggestionStatus = value;
    }

    [Obsolete("Use SuggestionStatus.")]
    public string RecommendationLevel
    {
        get => SuggestionStatus;
        set => SuggestionStatus = value;
    }
}
