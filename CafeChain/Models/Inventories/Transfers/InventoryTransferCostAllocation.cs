using CafeChain.Models.Inventories.Costing;

namespace CafeChain.Models.Inventories.Transfers;

public class InventoryTransferCostAllocation
{
    public long InventoryTransferCostAllocationId { get; set; }
    public int InventoryTransferDetailId { get; set; }
    public int SourceInventoryCostLayerId { get; set; }
    public decimal Quantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public virtual InventoryTransferDetail InventoryTransferDetail { get; set; } = null!;
    public virtual InventoryCostLayer SourceInventoryCostLayer { get; set; } = null!;
}
