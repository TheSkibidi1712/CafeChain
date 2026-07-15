using CafeChain.Models.Inventories.Ingredients;

namespace CafeChain.Models.Inventories.Suppliers
{
    public class IngredientSupplierPriceHistory
    {
        public int IngredientSupplierPriceHistoryId { get; set; }

        public int IngredientSupplierId { get; set; }

        /// <summary>Package price at EffectiveDate (not base-unit cost).</summary>
        public decimal Price { get; set; }

        /// <summary>Package content qty snapshot at EffectiveDate.</summary>
        public decimal? PackageQuantity { get; set; }

        /// <summary>Package content unit snapshot at EffectiveDate.</summary>
        public int? PackageUnitId { get; set; }

        public DateTime EffectiveDate { get; set; }

        public bool IsCurrent { get; set; }

        public string? Note { get; set; }

        public int? CreatedByStaffId { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public virtual IngredientSupplier IngredientSupplier { get; set; }

        public virtual Unit? PackageUnit { get; set; }
    }
}
