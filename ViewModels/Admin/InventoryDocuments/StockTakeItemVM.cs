namespace CafeChain.ViewModels.Admin.InventoryDocuments
{
    public class StockTakeItemVM
    {
        public int IngredientId { get; set; }
        public decimal SystemQty { get; set; } // tồn hệ thống
        public decimal ActualQty { get; set; } // tồn thực tế
    }
}
