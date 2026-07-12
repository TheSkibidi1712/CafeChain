using CafeChain.Models.Inventories.Production;
using CafeChain.Models.Inventories.Transactions;

namespace CafeChain.Models.Inventories.Costing
{
    /// <summary>
    /// Durable FIFO consume evidence for a ProductionRun input (#132).
    /// Separate from InventoryCostAllocation (document-bound) to avoid coupling.
    /// </summary>
    public class ProductionCostAllocation
    {
        public int ProductionCostAllocationId { get; set; }

        public int ProductionRunId { get; set; }

        /// <summary>PRODUCTION_OUT transaction that consumed this slice.</summary>
        public int InventoryTransactionId { get; set; }

        public int InventoryCostLayerId { get; set; }

        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public virtual ProductionRun ProductionRun { get; set; } = null!;
        public virtual InventoryTransaction InventoryTransaction { get; set; } = null!;
        public virtual InventoryCostLayer InventoryCostLayer { get; set; } = null!;
    }
}
