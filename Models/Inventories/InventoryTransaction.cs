using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories
{
    public class InventoryTransaction
    {
        public int InventoryTransactionId { get; set; }

        public int StoreInventoryId { get; set; }

        public int InventoryTransactionTypeId { get; set; }

        public decimal Quantity { get; set; }
        public decimal BeforeQty { get; set; }
        public decimal AfterQty { get; set; }
        public int? InventoryDocumentId { get; set; }
        public DateTime CreatedAt { get; set; }

        // ================= RELATION =================

        public virtual InventoryTransactionType TransactionType { get; set; }

        public virtual StoreInventory StoreInventory { get; set; }

        public virtual InventoryDocument? InventoryDocument { get; set; }
    }
}
