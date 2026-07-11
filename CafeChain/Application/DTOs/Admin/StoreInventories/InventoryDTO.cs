namespace CafeChain.Application.DTOs.Admin.StoreInventories
{
    public class InventoryDTO
    {
        public int StoreInventoryId { get; set; }
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string IngredientName { get; set; } = string.Empty;
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
    }
}
