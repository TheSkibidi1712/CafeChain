namespace CafeChain.Application.DTOs.Admin.InventoryDocuments
{
    public class InventoryDocumentDetailItemDTO
    {
        public int InventoryDocumentDetailId { get; set; }

        public int IngredientId { get; set; }

        public string IngredientName { get; set; }

        public int UnitId { get; set; }

        public string UnitName { get; set; }

        public decimal Quantity { get; set; }

        public decimal BaseQuantity { get; set; }

        public decimal? UnitPrice { get; set; }

        public decimal? CostPrice { get; set; }

        public decimal? CostAmount { get; set; }

        public decimal? TotalAmount { get; set; }

        public string? Note { get; set; }
    }
}
