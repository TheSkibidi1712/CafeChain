using CafeChain.Models.Inventories.Ingredients;

namespace CafeChain.Models.Inventories.Suppliers
{
    public class IngredientSupplier
    {
        public int IngredientSupplierId { get; set; }

        public int IngredientId { get; set; }

        public int SupplierId { get; set; }

        // đơn vị nội dung vật lý của gói mua (ADR-0005 / Issue #111)
        public int UnitId { get; set; }

        /// <summary>
        /// Lượng nội dung vật lý trong một gói mua (nullable legacy until remapped).
        /// </summary>
        public decimal? PackageQuantity { get; set; }

        // giá hiện tại của một gói mua
        public decimal CurrentPrice { get; set; }

        // MOQ
        public int? MinimumOrderPackageCount { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        [Obsolete("Use MinimumOrderPackageCount; MOQ is a package count.")]
        public decimal? MinimumOrderQuantity
        {
            get => MinimumOrderPackageCount;
            set => MinimumOrderPackageCount = value.HasValue ? decimal.ToInt32(value.Value) : null;
        }

        // thời gian giao
        public int? LeadTimeDays { get; set; }

        // NCC chính
        public bool IsPrimary { get; set; }

        public bool Active { get; set; }

        public bool AllowsLoosePurchase { get; set; }

        /// <summary>
        /// Price per procurement unit when this SKU is bought loose.
        /// CurrentPrice remains the price per package for Packaged mode.
        /// </summary>
        public decimal? CurrentProcurementUnitPrice { get; set; }

        public int? LooseProcurementUnitId { get; set; }

        /// <summary>
        /// Determines whether the loose unit price is derived from the package or entered independently.
        /// </summary>
        public string LoosePriceMode { get; set; } = LoosePurchasePriceModes.Independent;

        public decimal? LooseMinimumOrderQuantity { get; set; }

        public decimal? LooseQuantityStep { get; set; }

        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        [System.ComponentModel.DataAnnotations.Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        // ================= NAVIGATION =================

        public virtual Ingredient Ingredient { get; set; } = null!;

        public virtual Supplier Supplier { get; set; } = null!;

        public virtual Unit Unit { get; set; } = null!;
        public virtual Unit? LooseProcurementUnit { get; set; }

        public virtual ICollection<IngredientSupplierPriceHistory> PriceHistories { get; set; }
            = new List<IngredientSupplierPriceHistory>();
    }

    public static class LoosePurchasePriceModes
    {
        public const string Derived = "DERIVED";
        public const string Independent = "INDEPENDENT";

        public static bool IsValid(string? value) =>
            value == Derived || value == Independent;
    }
}
