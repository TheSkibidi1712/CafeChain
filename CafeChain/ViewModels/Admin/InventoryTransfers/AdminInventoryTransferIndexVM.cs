using CafeChain.Models.Enums.Inventory;
using CafeChain.ViewModels.Admin.InventoryDocuments.Dropdown;

namespace CafeChain.ViewModels.Admin.InventoryTransfers
{
    public class AdminInventoryTransferIndexVM
    {
        public string? Keyword { get; set; }
        public InventoryTransferStatus? Status { get; set; }
        public int? FromStoreId { get; set; }
        public int? ToStoreId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalItems { get; set; }
        public int TotalPages => PageSize <= 0
            ? 1
            : Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
        public List<StoreDropdownVM> Stores { get; set; } = [];
        public List<AdminInventoryTransferIndexItemVM> Items { get; set; } = [];
    }

    public class AdminInventoryTransferIndexItemVM
    {
        public int InventoryTransferId { get; set; }
        public string Code { get; set; } = string.Empty;
        public InventoryTransferStatus Status { get; set; }
        public InventoryTransferPurpose Purpose { get; set; }
        public string FromStoreName { get; set; } = string.Empty;
        public string ToStoreName { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime DocumentDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public int DetailCount { get; set; }
    }
}
