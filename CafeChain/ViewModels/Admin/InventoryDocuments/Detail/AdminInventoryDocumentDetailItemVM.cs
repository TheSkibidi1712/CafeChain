namespace CafeChain.ViewModels.Admin.InventoryDocuments.Detail
{
    public class AdminInventoryDocumentDetailItemVM
    {
        public string IngredientName { get; set; }

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
