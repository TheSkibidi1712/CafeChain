using CafeChain.Models.Orders;
using CafeChain.Models.Inventories.Transactions;

namespace CafeChain.Models.Inventories.Costing
{
    /// <summary>
    /// Durable FIFO sales cost slice for a committed POS order (#133).
    /// Separate from InventoryCostAllocation (document) and ProductionCostAllocation.
    /// </summary>
    public class SalesCostAllocation
    {
        public int SalesCostAllocationId { get; set; }

        public int OrderId { get; set; }
        public int OrderDetailId { get; set; }
        public int? OrderToppingId { get; set; }

        public int InventoryTransactionId { get; set; }
        public int InventoryCostLayerId { get; set; }

        public int? IngredientId { get; set; }
        public int? PreparedItemId { get; set; }

        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public virtual Order Order { get; set; } = null!;
        public virtual OrderDetail OrderDetail { get; set; } = null!;
        public virtual OrderTopping? OrderTopping { get; set; }
        public virtual InventoryTransaction InventoryTransaction { get; set; } = null!;
        public virtual InventoryCostLayer InventoryCostLayer { get; set; } = null!;
    }
}
