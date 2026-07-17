namespace CafeChain.Application.DTOs.Admin.Suppliers
{
    // DTO dùng cho danh sách (Index)
    public class AdminSupplierDTO
    {
        public int SupplierId { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? TaxCode { get; set; }
        public string? Address { get; set; }
        public string? Note { get; set; }
        public bool Active { get; set; }

        // Số điện thoại chính
        public string? PrimaryPhone { get; set; }

        // Người liên hệ chính
        public string? PrimaryContactName { get; set; }
        public string? PrimaryContactPhone { get; set; }

        public int ActiveOfferCount { get; set; }
        public int ActiveStoreCount { get; set; }
    }
}
