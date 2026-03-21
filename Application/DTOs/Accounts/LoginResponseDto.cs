namespace CafeChain.Application.DTOs.Accounts
{
    public class LoginResponseDto
    {
        public string Email { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; } // "Customer" or "Staff"
        public int AccountId { get; set; }
        public int? CustomerId { get; set; }
        public int? StaffId { get; set; }
    }
}
