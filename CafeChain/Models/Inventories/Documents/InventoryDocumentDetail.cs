using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Ingredients;

namespace CafeChain.Models.Inventories.Documents
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
        public decimal? CostPrice { get; set; }     // giá vốn tại thời điểm xuất
        public decimal? CostAmount { get; set; }    // = BaseQuantity * CostPrice

        public string Note { get; set; }
        public decimal? TotalAmount { get; set; }

        // Navigation
        public virtual Unit Unit { get; set; }
        public virtual InventoryDocument InventoryDocument { get; set; }
        public virtual Ingredient Ingredient { get; set; }
    }
}
