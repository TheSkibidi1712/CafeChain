using CafeChain.Models.Inventories.Costing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Costing
{
    public class ProductionCostAllocationConfiguration : IEntityTypeConfiguration<ProductionCostAllocation>
    {
        public void Configure(EntityTypeBuilder<ProductionCostAllocation> entity)
        {
            entity.ToTable("ProductionCostAllocations");

            entity.HasKey(x => x.ProductionCostAllocationId);

            entity.Property(x => x.Quantity)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.UnitCost)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.TotalCost)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.CreatedAtUtc)
                .HasColumnType("datetime2")
                .IsRequired();

            entity.HasOne(x => x.ProductionRun)
                .WithMany()
                .HasForeignKey(x => x.ProductionRunId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.InventoryTransaction)
                .WithMany()
                .HasForeignKey(x => x.InventoryTransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.InventoryCostLayer)
                .WithMany()
                .HasForeignKey(x => x.InventoryCostLayerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.ProductionRunId)
                .HasDatabaseName("IX_ProductionCostAllocations_ProductionRunId");

            entity.HasIndex(x => x.InventoryTransactionId);

            entity.HasIndex(x => x.InventoryCostLayerId);

            // Replay guard: same run cannot allocate the same layer twice
            entity.HasIndex(x => new { x.ProductionRunId, x.InventoryCostLayerId })
                .IsUnique()
                .HasDatabaseName("UX_ProductionCostAllocations_Run_Layer");

            entity.HasIndex(x => new { x.ProductionRunId, x.InventoryTransactionId, x.InventoryCostLayerId })
                .IsUnique()
                .HasDatabaseName("UX_ProductionCostAllocations_Run_Tx_Layer");
        }
    }
}
