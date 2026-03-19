using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories
{
    public class InventoryTransaction
    {
        public int InventoryTransactionId { get; set; }
        public int StoreInventoryId { get; set; }
        public int? StockImportId { get; set; }
        public int InventoryTransactionTypeId { get; set; }
        public decimal Quantity { get; set; }
        public string RefType { get; set; }
        public int? RefId { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual InventoryTransactionType TransactionType { get; set; }
        public virtual StoreInventory StoreInventory { get; set; }
        public virtual StockImport StockImport { get; set; }
    }
}
