using CafeChain.Models.Enums.Inventory;

namespace CafeChain.ViewModels.Admin.InventoryDocuments
{
    public class InventoryDocumentVM
    {
        public int StoreId { get; set; }
        public int StaffId { get; set; }

        public int? SupplierId { get; set; }

        public InventoryDocumentType Type { get; set; }
        public DateTime DocumentDate { get; set; }
        public string? Note { get; set; }

        public List<InventoryDocumentDetailCreateVM> Details { get; set; } = new();
    }
}
