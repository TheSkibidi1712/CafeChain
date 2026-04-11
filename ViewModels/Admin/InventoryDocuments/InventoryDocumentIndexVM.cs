using CafeChain.Models.Enums.Inventory;

namespace CafeChain.ViewModels.Admin.InventoryDocuments
{
    public class InventoryDocumentIndexVM
    {
        public List<InventoryDocumentItemVM> Items { get; set; } = new();

        public int TotalRecords { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }


        // Filter
        public string? Keyword { get; set; }
        public int? StoreId { get; set; }
        public InventoryDocumentType? Type { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}
