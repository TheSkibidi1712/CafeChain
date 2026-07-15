using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Models.Inventories.Documents;

public class InventoryDocumentSnapshot
{
    public int InventoryDocumentSnapshotId { get; set; }
    public int InventoryDocumentId { get; set; }
    public InventoryDocumentType Type { get; set; }
    public InventoryDocumentPurpose Purpose { get; set; }
    public InventoryDocumentStatus Status { get; set; }
    public long? NegativeApprovalId { get; set; }
    public decimal? BeforeQty { get; set; }
    public decimal? AfterQty { get; set; }
    public decimal? EffectiveMaxNegativeQty { get; set; }
    public string? PolicyVersion { get; set; }
    public bool CostComplete { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string StaffName { get; set; } = string.Empty;
    public string? PartnerName { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public virtual InventoryDocument InventoryDocument { get; set; } = null!;
    public virtual ICollection<InventoryDocumentSnapshotDetail> Details { get; set; } = [];
}
