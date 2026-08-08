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

            // SeedAll.sql is the authoritative, idempotent policy source.
        }
    }
}
