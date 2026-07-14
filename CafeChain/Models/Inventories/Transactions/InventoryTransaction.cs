using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Consolidation;
using CafeChain.Models.Inventories.Documents;
using CafeChain.Models.Inventories.Production;
using CafeChain.Models.Inventories.Refunds;
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
        public int? InventoryTransferDetailId { get; set; }
        public int? ReferenceOrderId { get; set; }

        /// <summary>Issue #120 — production run stock application linkage.</summary>
        public int? ProductionRunId { get; set; }

        /// <summary>
        /// Issue #121 — durable audit: exact ChildRecipeId (or parent sale recipe id for ingredient lines)
        /// used when building this sales movement. Not inventory identity.
        /// </summary>
        public int? SourceRecipeId { get; set; }

        /// <summary>Issue #123 — consolidation run linkage (new movements only; never reparent history).</summary>
        public int? InventoryConsolidationRunId { get; set; }

        /// <summary>Issue #128 — one BRANCH_RECEIPT_IN movement per BranchReceiptLine.</summary>
        public int? BranchReceiptLineId { get; set; }

        /// <summary>Issue #134 — full-order cash refund linkage (SALES_RETURN).</summary>
        public int? OrderRefundId { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual StoreInventory StoreInventory { get; set; }
        public virtual InventoryDocument? InventoryDocument { get; set; }
        public virtual InventoryTransfer? InventoryTransfer { get; set; }
        public virtual InventoryTransferDetail? InventoryTransferDetail { get; set; }
        public virtual Order? ReferenceOrder { get; set; }
        public virtual ProductionRun? ProductionRun { get; set; }
        public virtual Recipe? SourceRecipe { get; set; }
        public virtual InventoryConsolidationRun? InventoryConsolidationRun { get; set; }
        public virtual CafeChain.Models.Inventories.Stock.BranchReceiptLine? BranchReceiptLine { get; set; }
        public virtual OrderRefund? OrderRefund { get; set; }
    }
}
