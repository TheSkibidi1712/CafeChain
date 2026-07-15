using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Stores
{
    public sealed class StoreMenuItemAuditConfiguration : IEntityTypeConfiguration<StoreMenuItemAudit>
    {
        public void Configure(EntityTypeBuilder<StoreMenuItemAudit> entity)
        {
            entity.ToTable("StoreMenuItemAudits");
            entity.HasKey(x => x.StoreMenuItemAuditId);
            entity.Property(x => x.Action).IsRequired().HasMaxLength(50);
            entity.Property(x => x.OldDataJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.NewDataJson).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property(x => x.Reason).IsRequired().HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(x => x.StoreMenuItem).WithMany()
                .HasForeignKey(x => x.StoreMenuItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ActorStaff).WithMany()
                .HasForeignKey(x => x.ActorStaffId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.StoreMenuItemId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.StoreId, x.CreatedAtUtc });
        }
    }
}
