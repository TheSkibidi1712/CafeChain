using CafeChain.Models.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Permissions
{
    public class AccountPermissionOverrideConfiguration : IEntityTypeConfiguration<AccountPermissionOverride>
    {
        public void Configure(EntityTypeBuilder<AccountPermissionOverride> entity)
        {
            entity.ToTable("AccountPermissionOverrides");

            entity.HasKey(x => x.AccountPermissionOverrideId);

            // ================= PROPERTIES =================

            entity.Property(x => x.Effect)
                .IsRequired();

            entity.Property(x => x.Reason)
                .HasMaxLength(500);

            // ================= INDEX =================

            entity.HasIndex(x => new {x.AccountId, x.PermissionId})
                .IsUnique();

            // ================= RELATIONSHIPS =================

            entity.HasOne(x => x.Account)
                .WithMany()
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Permission)
                .WithMany(x => x.AccountPermissionOverrides)
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}