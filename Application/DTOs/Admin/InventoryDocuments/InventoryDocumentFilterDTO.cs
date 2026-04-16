using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Application.DTOs.Admin.InventoryDocuments
{
    public class InventoryDocumentFilterDTO
    {
        public string? Keyword { get; set; }

        // 🔥 QUAN TRỌNG: filter theo store trong scope
        public int? StoreId { get; set; }

        public InventoryDocumentType? Type { get; set; }

        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
