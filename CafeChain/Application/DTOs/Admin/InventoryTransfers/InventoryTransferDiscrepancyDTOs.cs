using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Application.DTOs.Admin.InventoryTransfers;

public sealed class InventoryTransferResolutionDTO
{
    public string? RowVersion { get; set; }
    public string? RequestKey { get; set; }
    public string? Reason { get; set; }
    public InventoryTransferDiscrepancyPostingType? ResolutionType { get; set; }
    public List<InventoryTransferResolutionLineDTO> Lines { get; set; } = [];
}

public sealed class InventoryTransferResolutionLineDTO
{
    public int InventoryTransferDetailId { get; set; }
    public decimal BaseQuantity { get; set; }
}

public sealed class InventoryTransferFollowUpDTO
{
    public string? RowVersion { get; set; }
    public string? RequestKey { get; set; }
    public string? Note { get; set; }
    public List<InventoryTransferResolutionLineDTO> Lines { get; set; } = [];
}

public sealed class InventoryTransferDiscrepancyDryRunRowDTO
{
    public int InventoryTransferId { get; set; }
    public string TransferCode { get; set; } = string.Empty;
    public int InventoryTransferDetailId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal DispatchedBaseQuantity { get; set; }
    public decimal DestinationAccepted { get; set; }
    public decimal DestinationRejected { get; set; }
    public decimal ReturnedToSource { get; set; }
    public decimal WrittenOff { get; set; }
    public decimal ClosedShortage { get; set; }
    public decimal InTransitOpen { get; set; }
    public string SuggestedStatus { get; set; } = string.Empty;
    public string TraceConfidence { get; set; } = string.Empty;
}
