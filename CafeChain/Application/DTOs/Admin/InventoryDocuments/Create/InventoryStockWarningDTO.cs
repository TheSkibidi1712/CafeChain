namespace CafeChain.Application.DTOs.Admin.InventoryDocuments.Create
{
    public class InventoryStockWarningDTO
    {
        public int StoreId { get; set; }

        public string StoreName { get; set; } = string.Empty;

        public int IngredientId { get; set; }

        public int? PreparedItemId { get; set; }

        public string ItemType { get; set; } = "INGREDIENT";

        public string IngredientName { get; set; } = string.Empty;

        public decimal AvailableQuantity { get; set; }

        public decimal ReservedQuantity { get; set; }

        public decimal UsableQuantity { get; set; }

        public decimal ThresholdQuantity { get; set; }

        public string UnitCode { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }
}
