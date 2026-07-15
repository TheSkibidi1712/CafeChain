using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Drinks.Toppings
{
    public class DrinkSizeToppingPolicyAuditConfiguration : IEntityTypeConfiguration<DrinkSizeToppingPolicyAudit>
    {
        public void Configure(EntityTypeBuilder<DrinkSizeToppingPolicyAudit> entity)
        {
            entity.ToTable("DrinkSizeToppingPolicyAudits");
            entity.HasKey(x => x.DrinkSizeToppingPolicyAuditId);
            entity.Property(x => x.Action).IsRequired().HasMaxLength(30);
            entity.Property(x => x.OldDataJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.NewDataJson).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property(x => x.Reason).HasMaxLength(500);
            entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne(x => x.Policy).WithMany()
                .HasForeignKey(x => x.DrinkSizeToppingPolicyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.DrinkSizeToppingPolicyId, x.CreatedAtUtc });
        }
    }
}
