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

    public sealed class AdminSupplierIndexPageDTO
    {
        public List<AdminSupplierDTO> Items { get; set; } = new();
        public string? Search { get; set; }
        public bool? Status { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalCount { get; set; }
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;
        public int FirstItemNumber => TotalCount == 0 ? 0 : ((PageIndex - 1) * PageSize) + 1;
        public int LastItemNumber => Math.Min(PageIndex * PageSize, TotalCount);

        public int SupplierCount { get; set; }
        public int ActiveSupplierCount { get; set; }
        public int ActiveOfferCount { get; set; }
        public int ActiveStoreCount { get; set; }
    }
}
