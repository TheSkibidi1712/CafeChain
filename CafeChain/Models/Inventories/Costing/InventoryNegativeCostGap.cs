namespace CafeChain.Models.Inventories.Costing;

public static class InventoryNegativeCostGapSources
{
    public const string PosSale = "POS_SALE";
    public const string ManualDocument = "MANUAL_DOCUMENT";
    public const string LegacyBalance = "LEGACY_BALANCE";
}

public static class InventoryNegativeCostGapStatuses
{
    public const string Open = "OPEN";
    public const string PartiallySettled = "PARTIALLY_SETTLED";
    public const string Settled = "SETTLED";
    public const string Cancelled = "CANCELLED";
}

public class InventoryNegativeCostGap
{
    public long InventoryNegativeCostGapId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public int StoreInventoryId { get; set; }
    public int? IngredientId { get; set; }
    public int? PreparedItemId { get; set; }
    public int? SalesCostGapId { get; set; }
    public int? InventoryDocumentDetailId { get; set; }
    public int? InventoryTransactionId { get; set; }
    public long? InventoryNegativeApprovalId { get; set; }
    public decimal OriginalQuantity { get; set; }
    public decimal OutstandingQuantity { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Status { get; set; } = InventoryNegativeCostGapStatuses.Open;
    public byte[] RowVersion { get; set; } = [];

    public virtual CafeChain.Models.Inventories.Transactions.InventoryTransaction? InventoryTransaction { get; set; }
    public virtual ICollection<InventoryCostGapSettlement> Settlements { get; set; } = [];
}
