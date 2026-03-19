namespace CafeChain.Models.Customers
{
    public class AccountType
    {
        public int AccountTypeId { get; set; } // PK

        public string Name { get; set; } // Customer | Staff
        public bool Active { get; set; }

        public virtual ICollection<Account> Accounts { get; set; }
    }
}
