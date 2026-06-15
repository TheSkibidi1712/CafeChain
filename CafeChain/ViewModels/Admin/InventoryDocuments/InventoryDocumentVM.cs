using CafeChain.Models.Enums.Inventory;

namespace CafeChain.ViewModels.Admin.InventoryDocuments
{
    public class InventoryDocumentVM
    {
        public int StoreId { get; set; }

        // ================= NGHIỆP VỤ =================
        public InventoryDocumentType Type { get; set; }
        public InventoryDocumentPurpose Purpose { get; set; }

        // ================= ĐỐI TƯỢNG =================
        public InventoryPartnerType PartnerType { get; set; }
        public int? PartnerId { get; set; }
        public string? PartnerName { get; set; }

        // ================= NCC =================
        public int? SupplierId { get; set; }

        public DateTime DocumentDate { get; set; }
        public string? Note { get; set; }

        public List<InventoryDocumentDetailCreateVM> Details { get; set; } = new();
    }
}
