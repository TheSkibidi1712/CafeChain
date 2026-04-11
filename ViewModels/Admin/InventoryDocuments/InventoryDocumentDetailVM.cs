namespace CafeChain.ViewModels.Admin.InventoryDocuments
{
    public class InventoryDocumentDetailVM
    {
        public int Id { get; set; }
        public string Code { get; set; }

        public string StoreName { get; set; }
        public string StaffName { get; set; }

        // ✅ Supplier ở HEADER
        public string? SupplierName { get; set; }

        public string Type { get; set; }
        public string Status { get; set; }

        public DateTime Date { get; set; }
        public string Note { get; set; }

        public decimal GrandTotal => Details.Sum(x => x.Total);

        public List<InventoryDocumentDetailItemVM> Details { get; set; } = new();
    }
}
