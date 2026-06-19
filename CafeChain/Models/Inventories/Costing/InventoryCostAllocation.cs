using CafeChain.Models.Inventories.Documents;

namespace CafeChain.Models.Inventories.Costing
{
    public class InventoryCostAllocation
    {
        public int InventoryCostAllocationId { get; set; }

        public int InventoryDocumentDetailId { get; set; }
        public int InventoryCostLayerId { get; set; }

        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }

        public virtual InventoryDocumentDetail InventoryDocumentDetail { get; set; }
        public virtual InventoryCostLayer InventoryCostLayer { get; set; }
    }
}
