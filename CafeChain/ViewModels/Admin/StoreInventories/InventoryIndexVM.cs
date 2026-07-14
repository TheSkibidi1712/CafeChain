namespace CafeChain.ViewModels.Admin.StoreInventories
{
    public class InventoryIndexVM
    {
        public int StoreId { get; set; }

        public string ActiveTab { get; set; } = CafeChain.Application.DTOs.Admin.StoreInventories.InventoryCatalogTypes.Ingredients;

        public string? Search { get; set; }

        public int Page { get; set; } = 1;

        public int TotalPages { get; set; }

        public int TotalCount { get; set; }

        public List<InventoryItemVM> Items { get; set; } = new();

        // Danh sách kho đã được lọc theo StaffScope.
        public List<InventoryStoreTabVM> Stores { get; set; } = new();
    }
}
