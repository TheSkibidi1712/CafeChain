namespace CafeChain.Models.Customers
{
    public class PasswordResetOtp
    {
        public int Id { get; set; }
        public int? AccountId { get; set; } // 🔥 nên có
        public string Email { get; set; }
        public string CodeHash { get; set; }

        public DateTime ExpiredAt { get; set; }
        public bool IsUsed { get; set; }
        public int FailedAttempts { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
