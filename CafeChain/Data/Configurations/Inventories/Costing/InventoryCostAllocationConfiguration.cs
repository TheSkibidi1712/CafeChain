using CafeChain.Models.Inventories.Costing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Costing
{
    public class InventoryCostAllocationConfiguration : IEntityTypeConfiguration<InventoryCostAllocation>
    {
        public void Configure(EntityTypeBuilder<InventoryCostAllocation> entity)
        {
            entity.ToTable("InventoryCostAllocations");

            entity.HasKey(x => x.InventoryCostAllocationId);

            // ================= BASIC =================

            entity.Property(x => x.Quantity)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.UnitCost)
                .HasColumnType("decimal(18,2)");

            // ================= RELATION =================

            entity.HasOne(x => x.InventoryDocumentDetail)
                .WithMany()
                .HasForeignKey(x => x.InventoryDocumentDetailId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.InventoryCostLayer)
                .WithMany()
                .HasForeignKey(x => x.InventoryCostLayerId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            entity.HasIndex(x => x.InventoryDocumentDetailId);

            entity.HasIndex(x => x.InventoryCostLayerId);

            entity.HasIndex(x => new
            {
                x.InventoryDocumentDetailId,
                x.InventoryCostLayerId
            });
        }
    }
}
