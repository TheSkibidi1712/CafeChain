namespace CafeChain.Application.DTOs.Accounts
{
    public class LoginResponseDto
    {
        public string Email { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; } // Role ưu tiên cao nhất (cho redirect)
        public List<string> AllRoles { get; set; } = new(); // 🔥 TẤT CẢ roles (cho Claims)
        public int AccountId { get; set; }
        public int? CustomerId { get; set; }
        public int? StaffId { get; set; }
        public int? StoreId { get; set; }
        public string? AvatarUrl { get; set; }
    }
}
