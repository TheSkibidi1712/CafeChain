using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Documents;
using CafeChain.Models.Inventories.Transfers;
using CafeChain.Models.Orders;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories.Transactions
{
    public class InventoryTransaction
    {
        public int InventoryTransactionId { get; set; }

        public int StoreInventoryId { get; set; }

        public InventoryTransactionTypeEnum Type { get; set; }

        public InventoryStockStatus StockStatus { get; set; }

        public decimal Quantity { get; set; }
        public decimal BeforeQty { get; set; }
        public decimal AfterQty { get; set; }
        public decimal? UnitCost { get; set; }
        public decimal? TotalCost { get; set; }

        public int? InventoryDocumentId { get; set; }
        public int? InventoryTransferId { get; set; }
        public int? ReferenceOrderId { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual StoreInventory StoreInventory { get; set; }
        public virtual InventoryDocument? InventoryDocument { get; set; }
        public virtual InventoryTransfer? InventoryTransfer { get; set; }
        public virtual Order? ReferenceOrder { get; set; }
    }
}
