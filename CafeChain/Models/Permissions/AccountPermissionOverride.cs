using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Permissions;

namespace CafeChain.Models.Permissions
{
    public class AccountPermissionOverride
    {
        public int AccountPermissionOverrideId { get; set; }

        public int AccountId { get; set; }

        public int PermissionId { get; set; }

        public PermissionEffect Effect { get; set; }

        public string? Reason { get; set; }

        public virtual Account Account { get; set; }

        public virtual Permission Permission { get; set; }
    }
}
