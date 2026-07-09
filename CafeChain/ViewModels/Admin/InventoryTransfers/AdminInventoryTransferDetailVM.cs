using CafeChain.Models.Enums.Inventory;

namespace CafeChain.ViewModels.Admin.InventoryTransfers
{
    public class AdminInventoryTransferDetailVM
    {
        public int InventoryTransferId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? RequestKey { get; set; }
        public InventoryTransferType Type { get; set; }
        public InventoryTransferPurpose Purpose { get; set; }
        public InventoryTransferStatus Status { get; set; }
        public DateTime DocumentDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string FromStoreName { get; set; } = string.Empty;
        public string ToStoreName { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public string? ConfirmedByName { get; set; }
        public string? CancelledByName { get; set; }
        public string? Note { get; set; }
        public List<AdminInventoryTransferDetailItemVM> Details { get; set; } = [];
    }

    public class AdminInventoryTransferDetailItemVM
    {
        public int InventoryTransferDetailId { get; set; }
        public string IngredientName { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public string UnitCode { get; set; } = string.Empty;
        public string BaseUnitCode { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal BaseQuantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? SourceBeforeQty { get; set; }
        public decimal? SourceAfterQty { get; set; }
        public decimal? DestinationBeforeQty { get; set; }
        public decimal? DestinationAfterQty { get; set; }
        public string? Note { get; set; }
    }
}
