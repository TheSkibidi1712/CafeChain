namespace CafeChain.Models.Customers
{
    public class PasswordResetOtp
    {
        public int Id { get; set; }

        public string Email { get; set; }
        public string Code { get; set; }

        public DateTime ExpiredAt { get; set; }
        public bool IsUsed { get; set; }
        public int FailedAttempts { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
