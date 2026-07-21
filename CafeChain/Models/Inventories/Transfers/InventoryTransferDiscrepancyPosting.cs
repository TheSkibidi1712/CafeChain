using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Staffs;

namespace CafeChain.Models.Inventories.Transfers;

/// <summary>
/// Immutable quantity and cost evidence for transfer discrepancies.
/// Accepted quantity remains authoritative on confirmed branch receipt lines.
/// </summary>
public class InventoryTransferDiscrepancyPosting
{
    public long InventoryTransferDiscrepancyPostingId { get; set; }
    public int InventoryTransferDetailId { get; set; }
    public long InventoryTransferCostAllocationId { get; set; }
    public long? RelatedPostingId { get; set; }
    public InventoryTransferDiscrepancyPostingType PostingType { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string RequestKey { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int ActorStaffId { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual InventoryTransferDetail InventoryTransferDetail { get; set; } = null!;
    public virtual InventoryTransferCostAllocation InventoryTransferCostAllocation { get; set; } = null!;
    public virtual InventoryTransferDiscrepancyPosting? RelatedPosting { get; set; }
    public virtual Staff ActorStaff { get; set; } = null!;
    public virtual ICollection<InventoryTransferDiscrepancyPosting> RelatedPostings { get; set; } = [];
    public virtual ICollection<InventoryCostLayer> ReturnCostLayers { get; set; } = [];
}
