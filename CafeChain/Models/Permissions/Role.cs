using CafeChain.Models.Customers;
using System.Collections;

namespace CafeChain.Models.Permissions
{
    public class Role
    {
        public int RoleId { get; set; }
        public string Name { get; set; }
        public bool Active { get; set; }
        public bool IsStoreLevel { get; set; }
        public DateTime CreatedAt { get; set; }


        // Navigation properties
        public virtual ICollection<AccountRole> AccountRoles { get; set; }
        public virtual ICollection<RolePermission> RolePermissions { get; set; }

    }
}
