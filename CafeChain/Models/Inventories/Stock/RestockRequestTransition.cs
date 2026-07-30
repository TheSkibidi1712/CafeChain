using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Staffs;
using CafeChain.Models.Inventories.Transfers;

namespace CafeChain.Models.Inventories.Stock
{
    /// <summary>
    /// Issue #128 — durable RestockRequest status transition history (not only final status).
    /// </summary>
    public class RestockRequestTransition
    {
        public int RestockRequestTransitionId { get; set; }

        public int RestockRequestId { get; set; }

        public string PreviousStatus { get; set; } = string.Empty;

        public string NewStatus { get; set; } = string.Empty;

        public int ActorStaffId { get; set; }

        public DateTime OccurredAtUtc { get; set; }

        public string? Reason { get; set; }

        public int? BranchReceiptId { get; set; }
        public int? InventoryTransferId { get; set; }

        public int? InventoryTransactionId { get; set; }

        public decimal? QuantityBefore { get; set; }

        public decimal? QuantityAfter { get; set; }

        public string? RequestKey { get; set; }

        /// <summary>
        /// Versioned, immutable business snapshot written when a deterministic
        /// reorder suggestion creates or adjusts this request.
        /// </summary>
        public string? SuggestionSnapshotVersion { get; set; }

        public string? SuggestionSnapshotJson { get; set; }

        public virtual RestockRequest RestockRequest { get; set; } = null!;
        public virtual Staff ActorStaff { get; set; } = null!;
        public virtual BranchReceipt? BranchReceipt { get; set; }
        public virtual InventoryTransfer? InventoryTransfer { get; set; }
        public virtual InventoryTransaction? InventoryTransaction { get; set; }
    }
}
