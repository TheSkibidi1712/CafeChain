using CafeChain.Models.Enums.Inventory;

namespace CafeChain.ViewModels.Admin.InventoryDocuments
{
    public class AdminInventoryDocumentListVM
    {
        public int InventoryDocumentId { get; set; }

        public string Code { get; set; }

        public InventoryDocumentType Type { get; set; }

        public InventoryDocumentStatus Status { get; set; }

        public InventoryDocumentPurpose Purpose { get; set; }

        public string StoreName { get; set; }

        public string? PartnerName { get; set; }

        public DateTime DocumentDate { get; set; }

        public decimal? FinalAmount { get; set; }

        public DateTime? ConfirmedAt { get; set; }
    }
}
