using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories
{
    public class InventoryTransaction
    {
        public int InvTId { get; set; }
        public int StoIId { get; set; }
        public int InventoryTransactionTypeId { get; set; }
        public decimal Quantity { get; set; }
        public string RefType { get; set; }
        public int? RefId { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual InventoryTransactionType TransactionType { get; set; }
        public virtual StoreInventory StoreInventory { get; set; }
    }
}
