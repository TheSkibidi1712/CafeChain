namespace CafeChain.ViewModels.Customers
{
    public class CustomerAddressViewModel
    {
        public int CustomerAddressId { get; set; }
        
        public string Address { get; set; } // Số nhà
        
        public string DisplayAddress { get; set; } // Chuỗi ghép hoàn chỉnh Tỉnh, Quận, Phường, Số nhà
        
        public int? ProvinceId { get; set; }
        public int? DistrictId { get; set; }
        public int? WardId { get; set; }

        public bool IsDefault { get; set; }
    }
}
