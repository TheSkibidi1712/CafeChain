namespace CafeChain.ViewModels.Admin.InventoryDocuments
{
    public class InventoryDocumentDetailItemVM
    {
        public string IngredientName { get; set; }

        public decimal Quantity { get; set; }
        public string UnitName { get; set; }
        public decimal BaseQuantity { get; set; }
        public string IngredientCode { get; set; }
        public string BaseUnitName { get; set; }
        public decimal? UnitPrice { get; set; }
        public string? Note { get; set; }
        public decimal? TotalAmount { get; set; }
    }
}
