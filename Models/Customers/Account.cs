using CafeChain.Models.Staffs;

namespace CafeChain.Models.Customers
{
    public class Account
    {
        public int AccountId { get; set; }

        public string Email { get; set; }
        public string PasswordHash { get; set; }

        public int AccountTypeId { get; set; } // FK

        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }

        // profile FK
        public int? CustomerId { get; set; }
        public int? StaffId { get; set; }

        public virtual AccountType AccountType { get; set; }

        public virtual Customer Customer { get; set; }
        public virtual Staff Staff { get; set; }
    }
}
