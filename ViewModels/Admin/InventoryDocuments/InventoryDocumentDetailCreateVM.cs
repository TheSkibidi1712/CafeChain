namespace CafeChain.ViewModels.Admin.InventoryDocuments
{
    public class InventoryDocumentDetailCreateVM
    {
        public int IngredientId { get; set; }

        public decimal Quantity { get; set; }

        public int UnitId { get; set; }

        public decimal? UnitPrice { get; set; }

        public string? Note { get; set; }


    }
}
