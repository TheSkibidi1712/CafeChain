using CafeChain.Models.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Permissions
{
    public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
    {
        public void Configure(EntityTypeBuilder<Permission> entity)
        {
            entity.ToTable("Permissions");

            entity.HasKey(x => x.PermissionId);

            // ================= PROPERTIES =================

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Action)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Description)
                .HasMaxLength(500);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // ================= INDEX =================

            entity.HasIndex(x => x.Code)
                .IsUnique();

            entity.HasIndex(x => new
            {
                x.PermissionGroupId,
                x.Action
            }).IsUnique();

            // ================= RELATIONSHIP =================

            entity.HasOne(x => x.PermissionGroup)
                .WithMany(x => x.Permissions)
                .HasForeignKey(x => x.PermissionGroupId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.RolePermissions)
                .WithOne(x => x.Permission)
                .HasForeignKey(x => x.PermissionId);

            entity.HasMany(x => x.AccountPermissionOverrides)
                .WithOne(x => x.Permission)
                .HasForeignKey(x => x.PermissionId);

            // ================= SEED =================

            entity.HasData(

                new Permission
                {
                    PermissionId = 1,
                    PermissionGroupId = 1,
                    Code = "Drink.View",
                    Name = "Xem đồ uống",
                    Action = "View",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                },

                new Permission
                {
                    PermissionId = 2,
                    PermissionGroupId = 1,
                    Code = "Drink.Create",
                    Name = "Thêm đồ uống",
                    Action = "Create",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                },

                new Permission
                {
                    PermissionId = 3,
                    PermissionGroupId = 1,
                    Code = "Drink.Update",
                    Name = "Cập nhật đồ uống",
                    Action = "Update",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                },

                new Permission
                {
                    PermissionId = 4,
                    PermissionGroupId = 1,
                    Code = "Drink.Delete",
                    Name = "Xóa đồ uống",
                    Action = "Delete",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1)
                }
            );
        }
    }
}