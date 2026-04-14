namespace CafeChain.ViewModels.Admin.StoreInventories
{
    public class InventoryIndexVM
    {
        public int StoreId { get; set; }

        public List<InventoryItemVM> Items { get; set; }
    }
}
