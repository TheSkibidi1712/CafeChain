namespace CafeChain.Application.DTOs.Admin.StoreInventories
{
    public static class InventoryCatalogTypes
    {
        public const string Ingredients = "ingredients";
        public const string PreparedItems = "prepared-items";
    }

    public class InventoryDTO
    {
        public int StoreInventoryId { get; set; }
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string IngredientName { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemType { get; set; } = InventoryCatalogTypes.Ingredients;
        public string IdentityBadge { get; set; } = string.Empty;
        public int? LegacyRecipeId { get; set; }
        public int? PreparedItemId { get; set; }
        public string QuantitySemanticsStatus { get; set; } = string.Empty;
        public decimal AvailableQty { get; set; }
        public decimal ReservedQty { get; set; }
        public decimal? MaxNegativeQty { get; set; }
        public DateTime LastUpdated { get; set; }
        public string UnitCode { get; set; } = string.Empty;
        public decimal? LastUnitPrice { get; set; }
        public string? LastSupplierName { get; set; }
        public int? LatestCostLayerId { get; set; }
        public DateTime? LatestCostLayerAt { get; set; }
        public int? SourceProductionRunId { get; set; }
        public string CostEvidenceStatus { get; set; } = string.Empty;
    }
}
