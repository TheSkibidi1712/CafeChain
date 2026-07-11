namespace CafeChain.Application.DTOs.Admin.InventoryDocuments.Create
{
    public class SupplierIngredientDTO
    {
        public int IngredientSupplierId { get; set; }

        public int IngredientId { get; set; }

        public string IngredientName { get; set; } = string.Empty;

        public int UnitId { get; set; }

        public string UnitName { get; set; } = string.Empty;

        /// <summary>Legacy field: package price (same as PackagePrice).</summary>
        public decimal CurrentPrice { get; set; }

        /// <summary>Price of one purchase package — not VND per g/ml.</summary>
        public decimal PackagePrice { get; set; }

        public decimal? PackageQuantity { get; set; }

        public int PackageUnitId { get; set; }

        public string PackageUnitCode { get; set; } = string.Empty;

        public string PackageUnitName { get; set; } = string.Empty;

        /// <summary>Package definition complete — not cost completeness (#117).</summary>
        public bool HasCompletePackageDefinition { get; set; }

        public decimal? MinimumOrderQuantity { get; set; }

        public int BaseUnitId { get; set; }

        public string BaseUnitName { get; set; } = string.Empty;

        public string BaseUnitCode { get; set; } = string.Empty;

        public string UnitCode { get; set; } = string.Empty;

        public decimal ConversionFactorToBase { get; set; }

        public bool CanConvertToBase { get; set; }

        public decimal AvailableBaseQuantity { get; set; }

        /// <summary>Null when package incomplete or PackageQuantity != 1. Full formula in #117.</summary>
        public decimal? SuggestedBaseUnitCost { get; set; }

        /// <summary>Null when package cannot auto-fill unit price (PackageQuantity != 1).</summary>
        public decimal? SuggestedUnitPrice { get; set; }

        public bool CanAutoFillUnitPrice { get; set; }

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
