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

            // SeedAll.sql is the authoritative, idempotent policy source.
        }
    }
}
