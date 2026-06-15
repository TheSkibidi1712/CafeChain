namespace CafeChain.Application.DTOs.Customer
{
    public class UpdateProfileRequest
    {
        public string FullName { get; set; }
        public DateTime? Dob { get; set; }
        public string PrimaryPhone { get; set; }
        public int? PrimaryAddressId { get; set; } // Đổi sang ID để xử lý chính xác
        public List<string> NewPhones { get; set; } = new List<string>();
        
        // 🔥 Cấu trúc hóa địa chỉ mới để chứa thông tin sau sát nhập
        public List<NewAddressDto> NewAddresses { get; set; } = new List<NewAddressDto>();
        public List<UpdateAddressDto> UpdatedAddresses { get; set; } = new List<UpdateAddressDto>();
    }

    public class NewAddressDto
    {
        public int TempId { get; set; } // Dùng map logic Tạm khi chưa có ID DB
        public string Street { get; set; }
        public int WardId { get; set; }
        public int DistrictId { get; set; }
        public int ProvinceId { get; set; }
    }

    public class UpdateAddressDto
    {
        public int CustomerAddressId { get; set; }
        public string Street { get; set; }
        public int WardId { get; set; }
        public int DistrictId { get; set; }
        public int ProvinceId { get; set; }
    }
}