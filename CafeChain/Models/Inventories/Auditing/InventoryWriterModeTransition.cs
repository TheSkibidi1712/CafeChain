using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories.Auditing
{
    public class InventoryWriterModeTransition
    {
        public int TransitionId { get; set; }
        public int StoreId { get; set; }
        public InventoryWriterMode FromMode { get; set; }
        public InventoryWriterMode ToMode { get; set; }
        public int ActorAccountId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? ReadinessHash { get; set; }
        public string? ReadinessSnapshotJson { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? AppliedAt { get; set; }
        public bool Succeeded { get; set; }
        public string? FailureCode { get; set; }

        public virtual Store Store { get; set; } = null!;
        public virtual Account ActorAccount { get; set; } = null!;
    }
}
