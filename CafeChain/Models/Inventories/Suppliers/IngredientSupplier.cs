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
        public decimal? MinimumOrderQuantity { get; set; }

        // thời gian giao
        public int? LeadTimeDays { get; set; }

        // NCC chính
        public bool IsPrimary { get; set; }

        public bool Active { get; set; }

        public string? Note { get; set; }

        // ================= NAVIGATION =================

        public virtual Ingredient Ingredient { get; set; } = null!;

        public virtual Supplier Supplier { get; set; } = null!;

        public virtual Unit Unit { get; set; } = null!;

        public virtual ICollection<IngredientSupplierPriceHistory> PriceHistories { get; set; }
            = new List<IngredientSupplierPriceHistory>();
    }
}
