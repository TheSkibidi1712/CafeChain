using CafeChain.Models.Staffs;

namespace CafeChain.Models.Customers
{
    public class Account
    {
        public int AccountId { get; set; }

        public string Email { get; set; }
        public string PasswordHash { get; set; }

        public bool Active { get; set; }
        // Legacy column retained for database compatibility. StaffHub no longer forces first-login password changes.
        public bool RequiresPasswordChange { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public virtual ICollection<AccountRole> AccountRoles { get; set; }
        public virtual Customer Customer { get; set; }
        public virtual Staff Staff { get; set; }
    }
}
