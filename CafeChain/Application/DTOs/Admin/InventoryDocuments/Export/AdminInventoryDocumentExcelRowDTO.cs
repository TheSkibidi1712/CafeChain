using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Application.DTOs.Admin.InventoryDocuments.Export
{
    public class AdminInventoryDocumentExcelRowDTO
    {
        public int No { get; set; }

        public string Code { get; set; } = string.Empty;

        public InventoryDocumentType Type { get; set; }

        public InventoryDocumentPurpose Purpose { get; set; }

        public string StoreName { get; set; } = string.Empty;

        public string? PartnerName { get; set; }

        public DateTime DocumentDate { get; set; }

        public decimal FinalAmount { get; set; }

        public InventoryDocumentStatus Status { get; set; }

        public DateTime? ConfirmedAt { get; set; }
    }
}
