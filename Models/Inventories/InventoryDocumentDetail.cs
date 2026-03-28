namespace CafeChain.Models.Inventories
{
    public class InventoryDocumentDetail
    {
        public int InventoryDocumentDetailId { get; set; }

        public int InventoryDocumentId { get; set; }
        public int IngredientId { get; set; }

        public decimal Quantity { get; set; }

        public string Unit { get; set; } // kg, thùng (input unit)

        public decimal? UnitPrice { get; set; } // dùng cho nhập

        public string Note { get; set; }

        // Navigation
        public virtual InventoryDocument InventoryDocument { get; set; }
        public virtual Ingredient Ingredient { get; set; }
    }
}
