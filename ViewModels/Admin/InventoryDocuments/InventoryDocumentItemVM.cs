using CafeChain.Models.Enums.Inventory;
namespace CafeChain.ViewModels.Admin.InventoryDocuments
{
    public class InventoryDocumentItemVM
    {
        public int Id { get; set; }
        public string Code { get; set; }

        public string StoreName { get; set; }

        // ✅ chỉ để hiển thị, không cho chọn
        public string StaffName { get; set; }

        public string? SupplierName { get; set; }
        public string? PartnerName { get; set; }
        public string IngredientCode { get; set; }
        public string BaseUnitName { get; set; }
        public InventoryDocumentPurpose Purpose { get; set; }
        public InventoryDocumentType Type { get; set; }
        public InventoryDocumentStatus Status { get; set; }

        public DateTime Date { get; set; }
        public string? Note { get; set; }

        public decimal TotalQuantity { get; set; }
        public decimal TotalAmount { get; set; }

        public List<InventoryDocumentDetailItemVM> Details { get; set; } = new();
    }
}
