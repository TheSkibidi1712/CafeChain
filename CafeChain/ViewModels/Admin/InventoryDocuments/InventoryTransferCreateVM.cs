namespace CafeChain.ViewModels.Admin.InventoryDocuments
{
    public class InventoryTransferCreateVM
    {
        public int FromStoreId { get; set; }
        public int ToStoreId { get; set; }

        public DateTime TransferDate { get; set; } = DateTime.Now;

        public string? Note { get; set; }

        public List<InventoryTransferItemVM> Items { get; set; } = new();
    }
}
