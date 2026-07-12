using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories.Stock
{
    /// <summary>
    /// Issue #128 — branch stock receipt authority. Only CONFIRMED posts inventory.
    /// </summary>
    public class BranchReceipt
    {
        public int BranchReceiptId { get; set; }

        public string ReceiptCode { get; set; } = string.Empty;

        public int StoreId { get; set; }

        public int? SupplierId { get; set; }

        /// <summary>DRAFT | CONFIRMED</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Idempotency key unique per store.</summary>
        public string ReceiptKey { get; set; } = string.Empty;

        public string? ReferenceNumber { get; set; }

        public DateTime ReceivedAt { get; set; }

        public int? ReceivedByStaffId { get; set; }

        public DateTime? ConfirmedAt { get; set; }

        public int? ConfirmedByStaffId { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }

        public int CreatedByStaffId { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public virtual Store Store { get; set; } = null!;
        public virtual Supplier? Supplier { get; set; }
        public virtual Staff? ReceivedByStaff { get; set; }
        public virtual Staff? ConfirmedByStaff { get; set; }
        public virtual Staff CreatedByStaff { get; set; } = null!;
        public virtual ICollection<BranchReceiptLine> Lines { get; set; } = new List<BranchReceiptLine>();
    }
}
