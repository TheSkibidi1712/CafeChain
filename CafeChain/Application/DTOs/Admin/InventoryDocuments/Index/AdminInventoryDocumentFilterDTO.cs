using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Application.DTOs.Admin.InventoryDocuments.Index
{
    public class AdminInventoryDocumentFilterDTO
    {
        public string? Search { get; set; }

        public InventoryDocumentType? Type { get; set; }

        public InventoryDocumentStatus? Status { get; set; }

        public InventoryDocumentPurpose? Purpose { get; set; }

        public int? StoreId { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }
}
