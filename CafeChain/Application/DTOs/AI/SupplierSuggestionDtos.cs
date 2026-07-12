using CafeChain.Models.Enums.Inventory;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.AI;

public sealed class SupplierSuggestionRequestDTO : IValidatableObject
{
    public InventoryDocumentType Type { get; set; }
    public InventoryDocumentPurpose Purpose { get; set; }
    [Range(1, int.MaxValue)] public int StoreId { get; set; }
    public DateTime DocumentDate { get; set; }
    public int? CurrentSupplierId { get; set; }
    [MinLength(1)] public List<SupplierSuggestionItemRequestDTO> Details { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Type != InventoryDocumentType.IMPORT || Purpose != InventoryDocumentPurpose.IMPORT_PURCHASE)
            yield return new ValidationResult("Chỉ phân tích nhà cung cấp cho phiếu nhập mua hàng.");
        if (DocumentDate == default)
            yield return new ValidationResult("Ngày nhập không hợp lệ.", [nameof(DocumentDate)]);
        if (Details.GroupBy(x => x.IngredientId).Any(x => x.Key > 0 && x.Count() > 1))
            yield return new ValidationResult("Không được gửi trùng nguyên liệu.", [nameof(Details)]);
    }
}

public sealed class SupplierSuggestionItemRequestDTO
{
    [Range(1, int.MaxValue)] public int IngredientId { get; set; }
    [Range(1, int.MaxValue)] public int UnitId { get; set; }
    [Range(typeof(decimal), "0.000001", "79228162514264337593543950335")] public decimal Quantity { get; set; }
}

public sealed class SupplierOfferDTO
{
    public int IngredientSupplierId { get; set; }
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public int PackageUnitId { get; set; }
    public string PackageUnitName { get; set; } = string.Empty;
    public int BaseUnitId { get; set; }
    public decimal? PackageQuantity { get; set; }
    public decimal PackagePrice { get; set; }
    public decimal? MinimumOrderQuantity { get; set; }
    public int? LeadTimeDays { get; set; }
    public bool IsPrimary { get; set; }
}

public sealed class SupplierSuggestionResultDTO
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? CurrentSupplierId { get; set; }
    public int? RecommendedSupplierId { get; set; }
    public string? RecommendedSupplierName { get; set; }
    public decimal? CurrentTotalCost { get; set; }
    public decimal? RecommendedTotalCost { get; set; }
    public decimal SavingsAmount { get; set; }
    public decimal SavingsPercentage { get; set; }
    public string RiskLevel { get; set; } = "High";
    public string Summary { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = "ReviewOnly";
    public List<string> Warnings { get; set; } = [];
    public List<SupplierComparisonDTO> Comparisons { get; set; } = [];
    public List<SupplierSuggestionApplyItemDTO> ApplyItems { get; set; } = [];
    public bool RequiresUserConfirmation { get; set; } = true;
    public bool UsedOllama { get; set; }
    public bool UsedFallback { get; set; }
}

public sealed class SupplierComparisonDTO
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public bool CoversAllIngredients { get; set; }
    public int CoveredIngredientCount { get; set; }
    public int TotalIngredientCount { get; set; }
    public List<string> MissingIngredients { get; set; } = [];
    public decimal? TotalCost { get; set; }
    public int? LeadTimeDays { get; set; }
    public string RiskLevel { get; set; } = "High";
    public List<string> Warnings { get; set; } = [];
}

public sealed class SupplierSuggestionApplyItemDTO
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public int UnitId { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal BaseQuantity { get; set; }
    public decimal MinimumOrderQuantity { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class SupplierExplanationDTO
{
    public string Summary { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = [];
    public string RecommendedAction { get; set; } = string.Empty;
}
