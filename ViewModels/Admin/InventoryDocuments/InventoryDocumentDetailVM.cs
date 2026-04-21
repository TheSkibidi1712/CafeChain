using CafeChain.Models.Enums.Inventory;

namespace CafeChain.ViewModels.Admin.InventoryDocuments
{
    public class InventoryDocumentDetailVM
    {
        public int Id { get; set; }
        public string Code { get; set; }

        public string StoreName { get; set; }

        // chỉ hiển thị
        public string StaffName { get; set; }

        public string? SupplierName { get; set; }
        public string? PartnerName { get; set; }
        public int? InventoryTransferId { get; set; }

        public InventoryDocumentPurpose Purpose { get; set; }
        public InventoryDocumentType Type { get; set; }
        public InventoryDocumentStatus Status { get; set; }

        public DateTime Date { get; set; }
        public string Note { get; set; }

        public decimal GrandTotal => Details.Sum(x => x.TotalAmount ?? 0);
        public List<InventoryDocumentDetailItemVM> Details { get; set; } = new();
    }
}
