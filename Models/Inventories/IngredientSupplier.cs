namespace CafeChain.Models.Inventories
{
    public class IngredientSupplier
    {
        public int IngredientSupplierId { get; set; }

        public int IngredientId { get; set; }
        public int SupplierId { get; set; }

        // 🔥 Giá hiện tại (giá tham khảo)
        public decimal Price { get; set; }

        // 🔥 Đơn vị nhập (kg, thùng, ...)
        public int UnitId { get; set; }

        public bool IsPrimary { get; set; } // NCC chính

        // Navigation
        public virtual Ingredient Ingredient { get; set; } = null!;
        public virtual Supplier Supplier { get; set; } = null!;
        public virtual Unit Unit { get; set; } = null!;
    }
}
