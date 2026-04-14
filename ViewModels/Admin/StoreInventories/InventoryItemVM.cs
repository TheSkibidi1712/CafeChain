namespace CafeChain.ViewModels.Admin.StoreInventories
{
    public class InventoryItemVM
    {
        public int StoreInventoryId { get; set; }
        public string IngredientName { get; set; }
        public decimal AvailableQty { get; set; }
        public decimal ReservedQty { get; set; }
        public DateTime LastUpdated { get; set; }
        public string UnitCode { get; set; }
    }
}
