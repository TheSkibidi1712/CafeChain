using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Staffs;

namespace CafeChain.Models.Inventories.Ice;

public class IceInventoryPosting
{
    public int IceInventoryPostingId { get; set; }
    public int IceAllocationId { get; set; }
    public int Revision { get; set; }
    public string PostingType { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public int? InventoryTransactionId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal? TotalCost { get; set; }
    public int ApprovedByStaffId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    public virtual IceAllocation IceAllocation { get; set; } = null!;
    public virtual InventoryTransaction? InventoryTransaction { get; set; }
    public virtual Staff ApprovedByStaff { get; set; } = null!;
}
