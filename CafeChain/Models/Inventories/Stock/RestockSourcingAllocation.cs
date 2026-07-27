using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Production;
using CafeChain.Models.Inventories.Transfers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Inventories.Ingredients;

namespace CafeChain.Models.Inventories.Stock;

/// <summary>
/// Durable source-of-supply authority for a replenishment request.
/// An active purchase allocation must point to a PA line or PO line.
/// </summary>
public class RestockSourcingAllocation
{
    public int RestockSourcingAllocationId { get; set; }
    public int RestockRequestId { get; set; }
    public string DecisionType { get; set; } = string.Empty;
    public decimal ProcurementQuantity { get; set; }
    public int ProcurementUnitId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? SourceDocumentType { get; set; }
    public int? SourceDocumentId { get; set; }
    public int? SourceDocumentLineId { get; set; }
    public int? PurchaseAdviceLineId { get; set; }
    public int? PurchaseOrderLineId { get; set; }
    public int? InventoryTransferId { get; set; }
    public int? ProductionRunId { get; set; }
    public string? Reason { get; set; }
    public int CreatedByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int? ReleasedByStaffId { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
    public string? ReleaseReason { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual RestockRequest RestockRequest { get; set; } = null!;
    public virtual Unit ProcurementUnit { get; set; } = null!;
    public virtual PurchaseAdviceLine? PurchaseAdviceLine { get; set; }
    public virtual PurchaseOrderLine? PurchaseOrderLine { get; set; }
    public virtual InventoryTransfer? InventoryTransfer { get; set; }
    public virtual ProductionRun? ProductionRun { get; set; }
    public virtual Staff CreatedByStaff { get; set; } = null!;
    public virtual Staff? ReleasedByStaff { get; set; }
}
