namespace CafeChain.ViewModels.Admin.InventoryDocuments
{
    public class InventoryDocumentDetailRowVM
    {
        public int IngredientId { get; set; }

        public string IngredientName { get; set; }

        public int UnitId { get; set; }

        public string UnitName { get; set; }

        public decimal Quantity { get; set; }

        public decimal? Price { get; set; }
    }
}
