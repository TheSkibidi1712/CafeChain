using CafeChain.Models.Staffs;

namespace CafeChain.Models.Customers
{
    public class AccountRole
    {
        public int AccountId { get; set; }
        public int RoleId { get; set; }

        public virtual Account Account { get; set; }
        public virtual Role Role { get; set; }
    }
}
