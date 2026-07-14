using CafeChain.Models.Inventories.Costing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Costing
{
    public class SalesCostGapConfiguration : IEntityTypeConfiguration<SalesCostGap>
    {
        public void Configure(EntityTypeBuilder<SalesCostGap> entity)
        {
            entity.ToTable("SalesCostGaps", table =>
            {
                table.HasCheckConstraint(
                    "CK_SalesCostGaps_ExactlyOneIdentity",
                    "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
            });

            entity.HasKey(x => x.SalesCostGapId);

            entity.Property(x => x.RequiredQuantity).HasColumnType("decimal(18,3)");
            entity.Property(x => x.AllocatedCostQuantity).HasColumnType("decimal(18,3)");
            entity.Property(x => x.MissingCostQuantity).HasColumnType("decimal(18,3)");
            entity.Property(x => x.ReasonCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").IsRequired();

            entity.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrderDetail)
                .WithMany()
                .HasForeignKey(x => x.OrderDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrderTopping)
                .WithMany()
                .HasForeignKey(x => x.OrderToppingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.OrderId);

            // One gap row per order line + inventory identity
            entity.HasIndex(x => new
                {
                    x.OrderId,
                    x.OrderDetailId,
                    x.OrderToppingId,
                    x.IngredientId,
                    x.PreparedItemId
                })
                .IsUnique()
                .HasDatabaseName("UX_SalesCostGaps_Order_Line_Identity");
        }
    }
}
