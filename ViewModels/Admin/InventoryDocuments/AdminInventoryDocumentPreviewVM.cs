namespace CafeChain.ViewModels.Admin.InventoryDocuments
{
    public class AdminInventoryDocumentPreviewVM
    {
        public string Code { get; set; }

        public DateTime DocumentDate { get; set; }

        public string StoreName { get; set; }

        public string StaffName { get; set; }

        public string? PartnerName { get; set; }

        public decimal TotalAmount { get; set; }

        public decimal VatAmount { get; set; }

        public decimal FinalAmount { get; set; }

        public List<AdminInventoryDocumentPreviewItemVM>
            Details
        { get; set; } = [];
    }
}
