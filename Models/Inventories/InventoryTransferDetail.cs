namespace CafeChain.Models.Inventories
{
    public class InventoryTransferDetail
    {
        public int InventoryTransferDetailId { get; set; }

        public int InventoryTransferId { get; set; }

        public int IngredientId { get; set; }

        // ===== SỐ LƯỢNG =====
        public decimal ExportQuantity { get; set; }     // số lượng xuất (base)
        public decimal ReceivedQuantity { get; set; }   // đã nhận

        // ===== OPTIONAL =====
        public decimal? UnitPrice { get; set; }
        public string? Note { get; set; }

        // ===== NAVIGATION =====
        public virtual InventoryTransfer InventoryTransfer { get; set; }
        public virtual Ingredient Ingredient { get; set; }
    }
}
