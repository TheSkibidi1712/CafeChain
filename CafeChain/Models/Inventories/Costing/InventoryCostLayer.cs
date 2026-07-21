using CafeChain.Models.Inventories.Production;
using CafeChain.Models.Inventories.Refunds;

namespace CafeChain.Models.Inventories.Costing
{
    /// <summary>
    /// FIFO cost evidence layer. Exactly one inventory identity:
    /// IngredientId XOR PreparedItemId (#132).
    /// </summary>
    public class InventoryCostLayer
    {
        public int InventoryCostLayerId { get; set; }

        /// <summary>Ingredient stock identity. Null when PreparedItem layer.</summary>
        public int? IngredientId { get; set; }

        /// <summary>PreparedItem stock identity. Null when Ingredient layer.</summary>
        public int? PreparedItemId { get; set; }

        public int StoreId { get; set; }

        public decimal Quantity { get; set; }
        public decimal RemainingQuantity { get; set; }

        public decimal UnitCost { get; set; }

        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Production output layer linkage: one layer per ProductionRun when set.
        /// </summary>
        public int? SourceProductionRunId { get; set; }

        /// <summary>
        /// Issue #134 — compensating layer created by full-order cash refund.
        /// Does not reprice original historical layers.
        /// </summary>
        public int? SourceOrderRefundId { get; set; }
        public int? SourceInventoryDocumentDetailId { get; set; }
        public int? SourceBranchReceiptLineId { get; set; }
        public long? SourceTransferCostAllocationId { get; set; }
        public long? SourceTransferDiscrepancyPostingId { get; set; }

        public virtual ProductionRun? SourceProductionRun { get; set; }
        public virtual OrderRefund? SourceOrderRefund { get; set; }
        public virtual CafeChain.Models.Inventories.Stock.BranchReceiptLine? SourceBranchReceiptLine { get; set; }
        public virtual CafeChain.Models.Inventories.Transfers.InventoryTransferCostAllocation? SourceTransferCostAllocation { get; set; }
        public virtual CafeChain.Models.Inventories.Transfers.InventoryTransferDiscrepancyPosting? SourceTransferDiscrepancyPosting { get; set; }
    }
}
