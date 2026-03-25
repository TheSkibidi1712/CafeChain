namespace CafeChain.Application.DTOs.Accounts
{
    public class RegisterDto
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string PhoneNumber { get; set; }
        public string Password { get; set; }
    }
}
