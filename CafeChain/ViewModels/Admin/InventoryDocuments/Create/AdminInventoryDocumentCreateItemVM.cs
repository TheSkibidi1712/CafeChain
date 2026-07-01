namespace CafeChain.ViewModels.Admin.InventoryDocuments.Create
{
    public class AdminInventoryDocumentCreateItemVM
    {
        public int IngredientSupplierId { get; set; }

        public int IngredientId { get; set; }

        public string IngredientName { get; set; } = string.Empty;

        public int UnitId { get; set; }

        public string UnitName { get; set; } = string.Empty;

        public decimal Quantity { get; set; }

        public decimal BaseQuantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal TotalAmount { get; set; }
    }
}
