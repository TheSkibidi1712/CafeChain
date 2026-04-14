namespace CafeChain.ViewModels.Admin.StoreInventories
{
    public class InventoryTransactionVM
    {
        public string IngredientName { get; set; }
        public string TypeName { get; set; }
        public decimal Quantity { get; set; }
        public decimal BeforeQty { get; set; }
        public decimal AfterQty { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UnitCode { get; set; }
    }
}
