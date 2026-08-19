namespace CafeChain.ViewModels.Admin.StoreInventories
{
    public class InventoryTransactionHistoryVM
    {
        public List<InventoryTransactionVM> Items { get; set; } = new();
        public List<InventoryStoreTabVM> Stores { get; set; } = new();

        public int StoreId { get; set; }
        public string? Search { get; set; }
        public string? TransactionType { get; set; }

        public int Page { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
    }
}
