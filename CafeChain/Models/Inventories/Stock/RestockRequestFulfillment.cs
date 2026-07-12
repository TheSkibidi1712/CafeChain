using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Staffs;

namespace CafeChain.Models.Inventories.Stock
{
    /// <summary>
    /// Issue #128 — links a RestockRequest to a planned fulfillment source (line-level).
    /// Does not mutate inventory.
    /// </summary>
    public class RestockRequestFulfillment
    {
        public int RestockRequestFulfillmentId { get; set; }

        public int RestockRequestId { get; set; }

        /// <summary>SUPPLIER | MANUAL (no transfer dual-post in #128).</summary>
        public string SourceType { get; set; } = string.Empty;

        /// <summary>Optional reuse of import source detail — not independently confirmable via dual-post.</summary>
        public int? InventoryDocumentDetailId { get; set; }

        public string Status { get; set; } = string.Empty;

        public decimal PlannedBaseQuantity { get; set; }

        public DateTime CreatedAt { get; set; }

        public int CreatedByStaffId { get; set; }

        public string? Notes { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual RestockRequest RestockRequest { get; set; } = null!;
        public virtual Staff CreatedByStaff { get; set; } = null!;
    }
}
