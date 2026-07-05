using CafeChain.ViewModels.Admin.InventoryDocuments.Dropdown;

namespace CafeChain.ViewModels.Admin.InventoryTransfers
{
    public class AdminInventoryTransferCreateVM
    {
        public DateTime DocumentDate { get; set; } = DateTime.Today;
        public List<StoreDropdownVM> Stores { get; set; } = [];
    }
}
