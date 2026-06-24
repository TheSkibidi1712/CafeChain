namespace CafeChain.Models.Permissions
{
    public class Permission
    {
        public int PermissionId { get; set; }

        public int PermissionGroupId { get; set; }

        // Drink.View
        public string Code { get; set; }

        // Xem đồ uống
        public string Name { get; set; }

        // View
        public string Action { get; set; }

        public string? Description { get; set; }

        public bool Active { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual PermissionGroup PermissionGroup { get; set; }

        public virtual ICollection<RolePermission> RolePermissions { get; set; }

        public virtual ICollection<AccountPermissionOverride> AccountPermissionOverrides { get; set; }
    }
}
