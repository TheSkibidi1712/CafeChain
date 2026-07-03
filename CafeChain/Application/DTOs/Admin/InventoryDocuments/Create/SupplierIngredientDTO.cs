namespace CafeChain.Application.DTOs.Admin.InventoryDocuments.Create
{
    public class SupplierIngredientDTO
    {
        public int IngredientSupplierId { get; set; }

        public int IngredientId { get; set; }

        public string IngredientName { get; set; } = string.Empty;

        public int UnitId { get; set; }

        public string UnitName { get; set; } = string.Empty;

        public decimal CurrentPrice { get; set; }

        public decimal? MinimumOrderQuantity { get; set; }

        public int BaseUnitId { get; set; }

        public string BaseUnitName { get; set; } = string.Empty;

        public string BaseUnitCode { get; set; } = string.Empty;

        public string UnitCode { get; set; } = string.Empty;

        public decimal ConversionFactorToBase { get; set; }

        public bool CanConvertToBase { get; set; }

        public decimal AvailableBaseQuantity { get; set; }

        public decimal SuggestedBaseUnitCost { get; set; }

        public decimal SuggestedUnitPrice { get; set; }

        public string PriceSource { get; set; } = string.Empty;

        public bool IsQuantityLocked { get; set; }

        public bool IsPriceLocked { get; set; }

        public List<InventoryIngredientUnitOptionDTO> UnitOptions { get; set; } = [];
    }

    public class InventoryIngredientUnitOptionDTO
    {
        public int UnitId { get; set; }

        public string UnitName { get; set; } = string.Empty;

        public string UnitCode { get; set; } = string.Empty;

        public decimal ConversionFactorToBase { get; set; }

        public bool IsBaseUnit { get; set; }
    }
}
