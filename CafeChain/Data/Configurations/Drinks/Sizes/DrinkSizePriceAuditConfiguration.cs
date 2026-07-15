using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Drinks.Sizes
{
    public class DrinkSizePriceAuditConfiguration : IEntityTypeConfiguration<DrinkSizePriceAudit>
    {
        public void Configure(EntityTypeBuilder<DrinkSizePriceAudit> entity)
        {
            entity.ToTable("DrinkSizePriceAudits");
            entity.HasKey(x => x.DrinkSizePriceAuditId);
            entity.Property(x => x.OldPrice).HasColumnType("decimal(18,2)");
            entity.Property(x => x.NewPrice).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Reason).IsRequired().HasMaxLength(500);
            entity.Property(x => x.CostStatus).IsRequired().HasMaxLength(60);
            entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(x => x.DrinkSize).WithMany(x => x.PriceAudits)
                .HasForeignKey(x => x.DrinkSizeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.DrinkSizeId, x.CreatedAtUtc });
        }
    }
}
