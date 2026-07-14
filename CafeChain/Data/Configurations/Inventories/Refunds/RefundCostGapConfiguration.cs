using CafeChain.Models.Inventories.Refunds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Refunds
{
    public class RefundCostGapConfiguration : IEntityTypeConfiguration<RefundCostGap>
    {
        public void Configure(EntityTypeBuilder<RefundCostGap> entity)
        {
            entity.ToTable("RefundCostGaps", table =>
            {
                table.HasCheckConstraint(
                    "CK_RefundCostGaps_ExactlyOneIdentity",
                    "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
            });

            entity.HasKey(x => x.RefundCostGapId);

            entity.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
            entity.Property(x => x.ReasonCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").IsRequired();

            entity.HasIndex(x => x.SalesCostGapId)
                .IsUnique()
                .HasDatabaseName("UX_RefundCostGaps_SalesCostGapId");

            entity.HasIndex(x => x.OrderRefundId);

            entity.HasOne(x => x.OrderRefund)
                .WithMany()
                .HasForeignKey(x => x.OrderRefundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SalesCostGap)
                .WithMany()
                .HasForeignKey(x => x.SalesCostGapId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
