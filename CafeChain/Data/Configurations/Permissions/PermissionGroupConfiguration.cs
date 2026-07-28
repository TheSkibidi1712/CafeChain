using CafeChain.Models.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Permissions
{
    public class PermissionGroupConfiguration : IEntityTypeConfiguration<PermissionGroup>
    {
        public void Configure(EntityTypeBuilder<PermissionGroup> entity)
        {
            entity.ToTable("PermissionGroups");

            entity.HasKey(x => x.PermissionGroupId);

            // ================= PROPERTIES =================

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.DisplayOrder)
                .HasDefaultValue(0);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            // ================= INDEX =================

            entity.HasIndex(x => x.Code)
                .IsUnique();

            entity.HasIndex(x => x.Name)
                .IsUnique();

            // ================= RELATIONSHIP =================

            entity.HasMany(x => x.Permissions)
                .WithOne(x => x.PermissionGroup)
                .HasForeignKey(x => x.PermissionGroupId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= SEED =================

            entity.HasData(
                new PermissionGroup
                {
                    PermissionGroupId = 1,
                    Code = "DRINK",
                    Name = "Quản lý đồ uống",
                    DisplayOrder = 1,
                    Active = true
                },
                new PermissionGroup
                {
                    PermissionGroupId = 2,
                    Code = "TOPPING",
                    Name = "Quản lý Topping",
                    DisplayOrder = 2,
                    Active = true
                },
                new PermissionGroup
                {
                    PermissionGroupId = 3,
                    Code = "ORDER",
                    Name = "Quản lý đơn hàng",
                    DisplayOrder = 3,
                    Active = true
                },
                new PermissionGroup
                {
                    PermissionGroupId = 4,
                    Code = "CUSTOMER",
                    Name = "Quản lý khách hàng",
                    DisplayOrder = 4,
                    Active = true
                },
                new PermissionGroup
                {
                    PermissionGroupId = 5,
                    Code = "SYSTEM",
                    Name = "Hệ thống",
                    DisplayOrder = 999,
                    Active = true
                },
                new PermissionGroup
                {
                    PermissionGroupId = 20,
                    Code = "OPERATIONAL_ICE",
                    Name = "Quản lý đá vận hành",
                    DisplayOrder = 20,
                    Active = true
                }
            );
        }
    }
}
