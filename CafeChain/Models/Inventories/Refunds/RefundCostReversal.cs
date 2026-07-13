using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Transactions;

namespace CafeChain.Models.Inventories.Refunds
{
    /// <summary>
    /// Durable reverse of one SalesCostAllocation via compensating layer (#134).
    /// </summary>
    public class RefundCostReversal
    {
        public int RefundCostReversalId { get; set; }

        public int OrderRefundId { get; set; }
        public int SalesCostAllocationId { get; set; }

        public int OriginalInventoryCostLayerId { get; set; }
        public int ReturnInventoryCostLayerId { get; set; }
        public int InventoryTransactionId { get; set; }

        public int? IngredientId { get; set; }
        public int? PreparedItemId { get; set; }

        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public virtual OrderRefund OrderRefund { get; set; } = null!;
        public virtual SalesCostAllocation SalesCostAllocation { get; set; } = null!;
        public virtual InventoryCostLayer OriginalInventoryCostLayer { get; set; } = null!;
        public virtual InventoryCostLayer ReturnInventoryCostLayer { get; set; } = null!;
        public virtual InventoryTransaction InventoryTransaction { get; set; } = null!;
    }
}
