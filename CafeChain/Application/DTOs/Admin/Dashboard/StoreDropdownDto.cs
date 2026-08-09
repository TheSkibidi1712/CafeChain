namespace CafeChain.Application.DTOs.Admin.Dashboard
{
    public class StoreDropdownDto
    {
        public int StoreId { get; set; }
        public string StoreName { get; set; }

        public int? ProvinceId { get; set; }
        public string ProvinceName { get; set; }

        public int? WardId { get; set; }
        public string WardName { get; set; }
    }
}
