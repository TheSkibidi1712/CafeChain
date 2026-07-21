namespace CafeChain.Application.DTOs.Admin.InventoryTransfers;

public sealed class InventoryTransferReceiveDTO
{
    public string? RowVersion { get; set; }
    public string? RequestKey { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
    public List<InventoryTransferReceiveLineDTO> Lines { get; set; } = [];
}

public sealed class InventoryTransferReceiveLineDTO
{
    public int InventoryTransferDetailId { get; set; }
    public decimal ReceivedBaseQuantity { get; set; }
    public decimal RejectedBaseQuantity { get; set; }
    public string? RejectionIssueType { get; set; }
    public string? RejectionReason { get; set; }
}
