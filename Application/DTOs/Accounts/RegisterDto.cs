using CafeChain.Models.Enums.Customer;
namespace CafeChain.Application.DTOs.Accounts
{
    public class RegisterDto
    {
        // ================= BASIC INFO =================
        public string FullName { get; set; } = string.Empty; 
        public string Email { get; set; } = string.Empty; 
        public string PhoneNumber { get; set; } = string.Empty; 
        public string Password { get; set; } = string.Empty; 
        // ================= OPTIONAL INFO =================
        public Gender Gender { get; set; } = Gender.Unknown; 
        public DateTime? DateOfBirth { get; set; } 
        public string? AvatarUrl { get; set; }
    }
}
