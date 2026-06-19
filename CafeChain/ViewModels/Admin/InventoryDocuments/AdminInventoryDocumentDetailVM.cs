using CafeChain.Models.Enums.Inventory;

namespace CafeChain.ViewModels.Admin.InventoryDocuments
{
    public class AdminInventoryDocumentDetailVM
    {
        public int InventoryDocumentId { get; set; }

        public string Code { get; set; }

        public InventoryDocumentType Type { get; set; }

        public InventoryDocumentStatus Status { get; set; }

        public InventoryDocumentPurpose Purpose { get; set; }

        public DateTime DocumentDate { get; set; }

        public string? RequestKey { get; set; }

        public bool IsProcessing { get; set; }

        public string StoreName { get; set; }

        public string StaffName { get; set; }

        public DateTime? ConfirmedAt { get; set; }

        public string? ConfirmedByName { get; set; }

        public InventoryPartnerType PartnerType { get; set; }

        public string? PartnerName { get; set; }

        public string? SupplierName { get; set; }

        public decimal? TotalAmount { get; set; }

        public decimal? VatAmount { get; set; }

        public decimal? FinalAmount { get; set; }

        public string? Note { get; set; }

        public List<AdminInventoryDocumentDetailItemVM>
            Details
        { get; set; } = [];
    }
}
