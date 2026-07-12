using CafeChain.Models.Enums.Inventory;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.AI;

public sealed class InventoryInputSuggestionRequestDTO : IValidatableObject
{
    public InventoryDocumentType Type { get; set; }
    public InventoryDocumentPurpose Purpose { get; set; }
    [Range(1, int.MaxValue)] public int StoreId { get; set; }
    public DateTime DocumentDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Type != InventoryDocumentType.IMPORT || Purpose != InventoryDocumentPurpose.IMPORT_PURCHASE)
            yield return new ValidationResult("Chỉ gợi ý dữ liệu cho phiếu nhập mua hàng.");
        if (DocumentDate == default)
            yield return new ValidationResult("Ngày nhập không hợp lệ.", [nameof(DocumentDate)]);
    }
}

public sealed class InventoryInputSuggestionResultDTO
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int StoreId { get; set; }
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = [];
    public List<InventoryInputSuggestionItemDTO> Items { get; set; } = [];
    public List<SupplierComparisonDTO> Comparisons { get; set; } = [];
    public bool CanApply { get; set; }
    public bool RequiresUserConfirmation { get; set; } = true;
    public bool UsedOllama { get; set; }
    public bool UsedFallback { get; set; }
}

public sealed class InventoryInputSuggestionItemDTO
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal AvailableQuantity { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal UsableQuantity { get; set; }
    public decimal MinimumStockLevel { get; set; }
    public decimal TargetStockLevel { get; set; }
    public decimal SuggestedBaseQuantity { get; set; }
    public int UnitId { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal MinimumOrderQuantity { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class InventoryInputExplanationDTO
{
    public string Summary { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = [];
}
