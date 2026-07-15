namespace CafeChain.Models.Inventories.Approvals;

public class InventoryNegativeApprovalLine
{
    public long InventoryNegativeApprovalLineId { get; set; }
    public long InventoryNegativeApprovalId { get; set; }
    public int InventoryDocumentDetailId { get; set; }
    public int StoreInventoryId { get; set; }
    public int? IngredientId { get; set; }
    public int? PreparedItemId { get; set; }
    public decimal BeforeQty { get; set; }
    public decimal IssueQty { get; set; }
    public decimal ProjectedAfterQty { get; set; }
    public decimal EffectiveMaxNegativeQty { get; set; }
    public byte[] InventoryRowVersion { get; set; } = [];

    public virtual InventoryNegativeApproval InventoryNegativeApproval { get; set; } = null!;
}
