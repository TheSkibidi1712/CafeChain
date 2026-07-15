using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories.Transfers
{
    public class InventoryTransfer
    {
        public int InventoryTransferId { get; set; }

        public string Code { get; set; } = null!;
        public string? RequestKey { get; set; }

        public int FromStoreId { get; set; }
        public int ToStoreId { get; set; }

        public InventoryTransferType Type { get; set; }
        public InventoryTransferPurpose Purpose { get; set; }
        public InventoryTransferStatus Status { get; set; }

        public DateTime DocumentDate { get; set; }

        public int CreatedByStaffId { get; set; }
        public int? ConfirmedByStaffId { get; set; }
        public int? CancelledByStaffId { get; set; }

        public DateTime? ConfirmedAt { get; set; }
        public DateTime? DispatchedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime CreatedAt { get; set; }

        public string? Note { get; set; }
        public byte[] RowVersion { get; set; } = [];

        public virtual Store FromStore { get; set; }
        public virtual Store ToStore { get; set; }
        public virtual Staff CreatedByStaff { get; set; }
        public virtual Staff? ConfirmedByStaff { get; set; }
        public virtual Staff? CancelledByStaff { get; set; }
        public virtual ICollection<InventoryTransferDetail> Details { get; set; } = new List<InventoryTransferDetail>();
    }
}
