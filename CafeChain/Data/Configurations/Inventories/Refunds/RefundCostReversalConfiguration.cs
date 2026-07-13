using CafeChain.Models.Inventories.Refunds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Refunds
{
    public class RefundCostReversalConfiguration : IEntityTypeConfiguration<RefundCostReversal>
    {
        public void Configure(EntityTypeBuilder<RefundCostReversal> entity)
        {
            entity.ToTable("RefundCostReversals", table =>
            {
                table.HasCheckConstraint(
                    "CK_RefundCostReversals_ExactlyOneIdentity",
                    "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
            });

            entity.HasKey(x => x.RefundCostReversalId);

            entity.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
            entity.Property(x => x.UnitCost).HasColumnType("decimal(18,2)");
            entity.Property(x => x.TotalCost).HasColumnType("decimal(18,2)");
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").IsRequired();

            entity.HasIndex(x => x.SalesCostAllocationId)
                .IsUnique()
                .HasDatabaseName("UX_RefundCostReversals_SalesCostAllocationId");

            entity.HasIndex(x => x.OrderRefundId);

            entity.HasOne(x => x.OrderRefund)
                .WithMany()
                .HasForeignKey(x => x.OrderRefundId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SalesCostAllocation)
                .WithMany()
                .HasForeignKey(x => x.SalesCostAllocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OriginalInventoryCostLayer)
                .WithMany()
                .HasForeignKey(x => x.OriginalInventoryCostLayerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReturnInventoryCostLayer)
                .WithMany()
                .HasForeignKey(x => x.ReturnInventoryCostLayerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.InventoryTransaction)
                .WithMany()
                .HasForeignKey(x => x.InventoryTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
