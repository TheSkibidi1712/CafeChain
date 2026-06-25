using System.Security;

namespace CafeChain.Models.Permissions
{
    public class PermissionGroup
    {
        public int PermissionGroupId { get; set; }

        public string Code { get; set; }

        public string Name { get; set; }

        public int DisplayOrder { get; set; }

        public bool Active { get; set; }

        public virtual ICollection<Permission> Permissions { get; set; }
    }
}
