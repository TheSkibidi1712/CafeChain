namespace CafeChain.ViewModels.Admin.StoreInventories
{
    public class InventoryIndexVM
    {
        public int StoreId { get; set; }

        public List<InventoryItemVM> Items { get; set; }

        // danh sách tab store
        public List<InventoryStoreTabVM> Stores { get; set; } = new();
    }
}
