namespace CafeChain.Models.Inventories.Costing;

public class InventoryCostGapSettlement
{
    public long InventoryCostGapSettlementId { get; set; }
    public long InventoryNegativeCostGapId { get; set; }
    public int InboundInventoryCostLayerId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual InventoryNegativeCostGap InventoryNegativeCostGap { get; set; } = null!;
    public virtual InventoryCostLayer InboundInventoryCostLayer { get; set; } = null!;
}
