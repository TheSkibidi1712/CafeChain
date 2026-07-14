using CafeChain.Models.Inventories.Costing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Costing
{
    public class SalesCostAllocationConfiguration : IEntityTypeConfiguration<SalesCostAllocation>
    {
        public void Configure(EntityTypeBuilder<SalesCostAllocation> entity)
        {
            entity.ToTable("SalesCostAllocations", table =>
            {
                table.HasCheckConstraint(
                    "CK_SalesCostAllocations_ExactlyOneIdentity",
                    "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
            });

            entity.HasKey(x => x.SalesCostAllocationId);

            entity.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
            entity.Property(x => x.UnitCost).HasColumnType("decimal(18,2)");
            entity.Property(x => x.TotalCost).HasColumnType("decimal(18,2)");
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

            entity.HasOne(x => x.InventoryTransaction)
                .WithMany()
                .HasForeignKey(x => x.InventoryTransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.InventoryCostLayer)
                .WithMany()
                .HasForeignKey(x => x.InventoryCostLayerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.OrderId);
            entity.HasIndex(x => x.OrderDetailId);
            entity.HasIndex(x => x.InventoryTransactionId);
            entity.HasIndex(x => x.InventoryCostLayerId);

            // Replay guard: one slice per order line attribution + movement + layer
            entity.HasIndex(x => new
                {
                    x.OrderId,
                    x.OrderDetailId,
                    x.OrderToppingId,
                    x.InventoryTransactionId,
                    x.InventoryCostLayerId
                })
                .IsUnique()
                .HasDatabaseName("UX_SalesCostAllocations_Order_Line_Tx_Layer");
        }
    }
}
