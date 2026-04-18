namespace CafeChain.ViewModels.Admin.InventoryDocuments
{
    public class InventoryTransferItemVM
    {
        public int IngredientId { get; set; }

        public decimal Quantity { get; set; }
        public int UnitId { get; set; }

        public decimal? UnitPrice { get; set; } // optional (nếu cần tracking giá)

        public string? Note { get; set; }
    }
}
