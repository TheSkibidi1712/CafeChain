namespace CafeChain.ViewModels.Admin.StoreInventories
{
    public class InventoryIndexVM
    {
        public int StoreId { get; set; }

        public List<InventoryItemVM> Items { get; set; } = new();

        // Danh sách kho đã được lọc theo StaffScope.
        public List<InventoryStoreTabVM> Stores { get; set; } = new();
    }
}
