using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Procurement;

public sealed class ExplainReorderSuggestionRequest
{
    [Range(1, int.MaxValue)]
    public int StoreId { get; set; }

    [Range(1, int.MaxValue)]
    public int IngredientId { get; set; }

    [Required, StringLength(4096, MinimumLength = 16)]
    public string SuggestionToken { get; set; } = string.Empty;
}

public sealed class ConfirmReorderSuggestionRequest
{
    [Range(1, int.MaxValue)]
    public int StoreId { get; set; }

    [Range(1, int.MaxValue)]
    public int IngredientId { get; set; }

    [Required, StringLength(4096, MinimumLength = 16)]
    public string SuggestionToken { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 32)]
    public string RequestKey { get; set; } = string.Empty;
}

public sealed class ConfirmReorderSuggestionResultDto
{
    public int RestockRequestId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public bool Replayed { get; set; }
    public string Message { get; set; } = string.Empty;
}

public static class ReorderSuggestionConfirmationOperations
{
    public const string Created = "CREATED";
    public const string Adjusted = "ADJUSTED";
}

public static class ReorderSuggestionConfirmationErrorCodes
{
    public const string InvalidRequest = "REORDER_CONFIRM_INVALID_REQUEST";
    public const string Unauthorized = "REORDER_CONFIRM_UNAUTHORIZED";
    public const string SuggestionExpired = "REORDER_SUGGESTION_EXPIRED";
    public const string SuggestionChanged = "REORDER_SUGGESTION_CHANGED";
    public const string DataIncomplete = "REORDER_DATA_INCOMPLETE";
    public const string NoRemainingDemand = "REORDER_NO_REMAINING_DEMAND";
    public const string RequestInProgress = "REORDER_CONFIRM_IN_PROGRESS";
    public const string ConcurrentUpdate = "REORDER_CONFIRM_CONCURRENT_UPDATE";
}

public sealed class ReorderSuggestionTokenPayload
{
    public int IssuedToStaffId { get; set; }
    public int StoreId { get; set; }
    public int IngredientId { get; set; }
    public int AnalysisWindowDays { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string CalculationVersion { get; set; } = string.Empty;
    public string DecisionFingerprint { get; set; } = string.Empty;
}

public sealed class ReorderSuggestionDecisionFingerprintDto
{
    public int StoreId { get; set; }
    public int IngredientId { get; set; }
    public string CalculationVersion { get; set; } = string.Empty;
    public decimal OnHandQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal AvailableStock { get; set; }
    public decimal? MinimumStock { get; set; }
    public decimal? AverageDailyConsumption { get; set; }
    public int? LeadTimeDays { get; set; }
    public decimal? ReorderPoint { get; set; }
    public decimal IncomingQuantity { get; set; }
    public decimal? RawDemand { get; set; }
    public decimal ProcurementCoveredQuantity { get; set; }
    public decimal? RemainingDemand { get; set; }
    public decimal? SuggestedPackageQuantity { get; set; }
    public decimal? FinalSuggestedQuantity { get; set; }
    public int? IngredientSupplierId { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public int? PackageUnitId { get; set; }
    public decimal? PackageBaseQuantity { get; set; }
    public int? MinimumOrderPackageCount { get; set; }
    public decimal? SupplierPrice { get; set; }
    public DateTime? PriceEffectiveAtUtc { get; set; }
    public decimal? EstimatedCost { get; set; }
    public string SuggestionStatus { get; set; } = string.Empty;
    public int? ActiveRestockRequestId { get; set; }
    public IReadOnlyList<string> ReasonCodes { get; set; } = Array.Empty<string>();
}

public sealed class ReorderSuggestionBusinessSnapshot
{
    public const string SchemaVersion = "1";

    public int StoreId { get; set; }
    public int IngredientId { get; set; }
    public DateTime AnalysisFromUtc { get; set; }
    public DateTime AnalysisToUtc { get; set; }
    public DateTime CalculatedAtUtc { get; set; }
    public string CalculationVersion { get; set; } = string.Empty;
    public decimal OnHandQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal AvailableStock { get; set; }
    public decimal? MinimumStock { get; set; }
    public decimal? AverageDailyConsumption { get; set; }
    public int? LeadTimeDays { get; set; }
    public decimal? ReorderPoint { get; set; }
    public decimal IncomingQuantity { get; set; }
    public decimal ProcurementCoveredQuantity { get; set; }
    public decimal? RawDemand { get; set; }
    public decimal? RemainingDemand { get; set; }
    public decimal? SuggestedPackageQuantity { get; set; }
    public decimal? FinalSuggestedQuantity { get; set; }
    public int? IngredientSupplierId { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public decimal? PackageQuantity { get; set; }
    public int? PackageUnitId { get; set; }
    public int? MinimumOrderPackageCount { get; set; }
    public decimal? SupplierPrice { get; set; }
    public DateTime? PriceEffectiveAtUtc { get; set; }
    public decimal? EstimatedCost { get; set; }
    public string SuggestionStatus { get; set; } = string.Empty;
    public string SuggestionReason { get; set; } = string.Empty;
    public IReadOnlyList<string> ReasonCodes { get; set; } = Array.Empty<string>();
    public string Source { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
}

public static class ReorderSuggestionContractMapper
{
    public static ReorderSuggestionDecisionFingerprintDto ToDecision(
        ReorderSuggestionItemDto item) =>
        new()
        {
            StoreId = item.StoreId,
            IngredientId = item.IngredientId,
            CalculationVersion = item.CalculationVersion,
            OnHandQuantity = item.OnHandQuantity,
            ReservedQuantity = item.ReservedQuantity,
            AvailableStock = item.AvailableStock,
            MinimumStock = item.MinimumStock,
            AverageDailyConsumption = item.AverageDailyConsumption,
            LeadTimeDays = item.LeadTimeDays,
            ReorderPoint = item.ReorderPoint,
            IncomingQuantity = item.IncomingQuantity,
            RawDemand = item.RawDemand,
            ProcurementCoveredQuantity = item.ProcurementCoveredQuantity,
            RemainingDemand = item.RemainingDemand,
            SuggestedPackageQuantity = item.SuggestedPackageCount,
            FinalSuggestedQuantity = item.FinalSuggestedQuantity,
            IngredientSupplierId = item.IngredientSupplierId,
            SupplierId = item.SupplierId,
            SupplierName = item.SupplierName,
            PackageUnitId = item.PackageUnitId,
            PackageBaseQuantity = item.PackageBaseQuantity,
            MinimumOrderPackageCount = item.MinimumOrderPackageCount,
            SupplierPrice = item.PackagePrice,
            PriceEffectiveAtUtc = item.PriceEffectiveAtUtc,
            EstimatedCost = item.EstimatedCost,
            SuggestionStatus = item.SuggestionStatus,
            ActiveRestockRequestId = item.ActiveRestockRequestId,
            ReasonCodes = item.ReasonCodes
        };

    public static ReorderSuggestionBusinessSnapshot ToSnapshot(
        ReorderSuggestionItemDto item,
        string source,
        string operation) =>
        new()
        {
            StoreId = item.StoreId,
            IngredientId = item.IngredientId,
            AnalysisFromUtc = item.AnalysisFromUtc,
            AnalysisToUtc = item.AnalysisToUtc,
            CalculatedAtUtc = item.CalculatedAtUtc,
            CalculationVersion = item.CalculationVersion,
            OnHandQuantity = item.OnHandQuantity,
            ReservedQuantity = item.ReservedQuantity,
            AvailableStock = item.AvailableStock,
            MinimumStock = item.MinimumStock,
            AverageDailyConsumption = item.AverageDailyConsumption,
            LeadTimeDays = item.LeadTimeDays,
            ReorderPoint = item.ReorderPoint,
            IncomingQuantity = item.IncomingQuantity,
            ProcurementCoveredQuantity = item.ProcurementCoveredQuantity,
            RawDemand = item.RawDemand,
            RemainingDemand = item.RemainingDemand,
            SuggestedPackageQuantity = item.SuggestedPackageCount,
            FinalSuggestedQuantity = item.FinalSuggestedQuantity,
            IngredientSupplierId = item.IngredientSupplierId,
            SupplierId = item.SupplierId,
            SupplierName = item.SupplierName,
            PackageQuantity = item.PackageBaseQuantity,
            PackageUnitId = item.PackageUnitId,
            MinimumOrderPackageCount = item.MinimumOrderPackageCount,
            SupplierPrice = item.PackagePrice,
            PriceEffectiveAtUtc = item.PriceEffectiveAtUtc,
            EstimatedCost = item.EstimatedCost,
            SuggestionStatus = item.SuggestionStatus,
            SuggestionReason = item.Reason,
            ReasonCodes = item.ReasonCodes,
            Source = source,
            Operation = operation
        };
}
