using CafeChain.ViewModels.Admin.InventoryDocuments.Dropdown;

namespace CafeChain.ViewModels.Admin.InventoryTransfers
{
    public class AdminInventoryTransferCreateVM
    {
        public DateTime DocumentDate { get; set; } = DateTime.Today;
        public string CreatedByName { get; set; } = "Không xác định";
        public List<StoreDropdownVM> Stores { get; set; } = [];
    }
}
