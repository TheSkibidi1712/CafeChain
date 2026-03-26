namespace CafeChain.Application.DTOs.Customer
{
    public class UpdateProfileRequest
    {
        public string FullName { get; set; }
        public DateTime? Dob { get; set; }
        // 🔥 THÊM 2 DÒNG NÀY ĐỂ HỨNG DỮ LIỆU TỪ JAVASCRIPT 🔥
        public string PrimaryPhone { get; set; }
        public string PrimaryAddress { get; set; }
        public List<string> NewPhones { get; set; } = new List<string>();
        public List<string> NewAddresses { get; set; } = new List<string>();
    }
}