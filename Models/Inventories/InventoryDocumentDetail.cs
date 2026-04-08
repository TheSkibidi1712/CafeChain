using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Models.Inventories
{
    public class InventoryDocumentDetail
    {
        public int InventoryDocumentDetailId { get; set; }

        public int InventoryDocumentId { get; set; }
        public int IngredientId { get; set; }
        public decimal Quantity { get; set; }
        public decimal BaseQuantity { get; set; }

        public int UnitId { get; set; } // dùng cho nhập, có thể khác với đơn vị cơ sở của nguyên liệu

        public decimal? UnitPrice { get; set; } // dùng cho nhập

        public string Note { get; set; }

        // Navigation
        public virtual Unit Unit { get; set; }
        public virtual InventoryDocument InventoryDocument { get; set; }
        public virtual Ingredient Ingredient { get; set; }
    }
}
