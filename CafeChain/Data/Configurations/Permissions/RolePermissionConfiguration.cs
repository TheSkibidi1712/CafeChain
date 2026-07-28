using CafeChain.Models.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Permissions
{
    public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
    {
        public void Configure(EntityTypeBuilder<RolePermission> entity)
        {
            entity.ToTable("RolePermissions");

            entity.HasKey(x => new
            {
                x.RoleId,
                x.PermissionId
            });

            entity.HasOne(x => x.Role)
                .WithMany(x => x.RolePermissions)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Permission)
                .WithMany(x => x.RolePermissions)
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasData(
                new RolePermission { RoleId = 1, PermissionId = 1 },
                new RolePermission { RoleId = 1, PermissionId = 2 },
                new RolePermission { RoleId = 1, PermissionId = 3 },
                new RolePermission { RoleId = 1, PermissionId = 4 },
                new RolePermission { RoleId = 1, PermissionId = 27 },
                new RolePermission { RoleId = 1, PermissionId = 200 },
                new RolePermission { RoleId = 1, PermissionId = 201 },
                new RolePermission { RoleId = 1, PermissionId = 202 },
                new RolePermission { RoleId = 1, PermissionId = 203 },
                new RolePermission { RoleId = 2, PermissionId = 200 },
                new RolePermission { RoleId = 3, PermissionId = 200 },
                new RolePermission { RoleId = 3, PermissionId = 201 },
                new RolePermission { RoleId = 3, PermissionId = 202 },
                new RolePermission { RoleId = 3, PermissionId = 203 },
                new RolePermission { RoleId = 4, PermissionId = 200 },
                new RolePermission { RoleId = 5, PermissionId = 200 },
                new RolePermission { RoleId = 5, PermissionId = 201 },
                new RolePermission { RoleId = 5, PermissionId = 202 },
                new RolePermission { RoleId = 5, PermissionId = 203 },
                new RolePermission { RoleId = 6, PermissionId = 200 },
                new RolePermission { RoleId = 6, PermissionId = 201 },
                new RolePermission { RoleId = 6, PermissionId = 202 },
                new RolePermission { RoleId = 6, PermissionId = 203 },
                new RolePermission { RoleId = 8, PermissionId = 200 },
                new RolePermission { RoleId = 8, PermissionId = 201 }
            );
        }
    }
}
