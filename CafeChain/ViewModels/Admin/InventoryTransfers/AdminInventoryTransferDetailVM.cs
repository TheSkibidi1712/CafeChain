using CafeChain.Models.Enums.Inventory;

namespace CafeChain.ViewModels.Admin.InventoryTransfers
{
    public class AdminInventoryTransferDetailVM
    {
        public int InventoryTransferId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? RequestKey { get; set; }
        public string RowVersion { get; set; } = string.Empty;
        public InventoryTransferType Type { get; set; }
        public InventoryTransferPurpose Purpose { get; set; }
        public InventoryTransferStatus Status { get; set; }
        public DateTime DocumentDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public int FromStoreId { get; set; }
        public int ToStoreId { get; set; }
        public string FromStoreName { get; set; } = string.Empty;
        public string ToStoreName { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public string? ConfirmedByName { get; set; }
        public string? CancelledByName { get; set; }
        public string? Note { get; set; }
        public int? ParentInventoryTransferId { get; set; }
        public string? ParentTransferCode { get; set; }
        public bool CanReceive { get; set; }
        public bool CanRequestReturn { get; set; }
        public bool CanConfirmReturn { get; set; }
        public bool CanResolveShortage { get; set; }
        public bool CanCreateFollowUp { get; set; }
        public List<AdminInventoryTransferDetailItemVM> Details { get; set; } = [];
        public List<AdminInventoryTransferTimelineItemVM> Timeline { get; set; } = [];
    }

    public class AdminInventoryTransferDetailItemVM
    {
        public int InventoryTransferDetailId { get; set; }
        public int? IngredientId { get; set; }
        public int? PreparedItemId { get; set; }
        public int? RestockRequestId { get; set; }
        public string ItemType { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public string UnitCode { get; set; } = string.Empty;
        public string BaseUnitCode { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal BaseQuantity { get; set; }
        public decimal DispatchedBaseQuantity { get; set; }
        public decimal DestinationAccepted { get; set; }
        public decimal DestinationRejected { get; set; }
        public decimal ReturnedToSource { get; set; }
        public decimal WrittenOff { get; set; }
        public decimal ClosedShortage { get; set; }
        public decimal InTransitOpen { get; set; }
        public decimal PendingReturn { get; set; }
        public decimal ReturnableRejected { get; set; }
        public string DiscrepancyStatus { get; set; } = string.Empty;
        public decimal? UnitPrice { get; set; }
        public decimal? SourceBeforeQty { get; set; }
        public decimal? SourceAfterQty { get; set; }
        public decimal? DestinationBeforeQty { get; set; }
        public decimal? DestinationAfterQty { get; set; }
        public string? Note { get; set; }
    }

    public class AdminInventoryTransferTimelineItemVM
    {
        public DateTime OccurredAt { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string UnitCode { get; set; } = string.Empty;
        public string ActorName { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public string? RequestKey { get; set; }
    }
}
