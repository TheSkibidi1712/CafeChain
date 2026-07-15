using CafeChain.Models.Inventories.Transfers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Transfers;

public sealed class InventoryTransferCostAllocationConfiguration : IEntityTypeConfiguration<InventoryTransferCostAllocation>
{
    public void Configure(EntityTypeBuilder<InventoryTransferCostAllocation> entity)
    {
        entity.ToTable("InventoryTransferCostAllocations", table => table.HasCheckConstraint(
            "CK_InventoryTransferCostAllocation_Quantity",
            "[Quantity] > 0 AND [ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [Quantity] AND [UnitCost] > 0"));
        entity.HasKey(x => x.InventoryTransferCostAllocationId);
        entity.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
        entity.Property(x => x.ReceivedQuantity).HasColumnType("decimal(18,3)");
        entity.Property(x => x.UnitCost).HasColumnType("decimal(18,6)");
        entity.Property(x => x.TotalCost).HasColumnType("decimal(18,2)");
        entity.Property(x => x.RowVersion).IsRowVersion();
        entity.HasIndex(x => new { x.InventoryTransferDetailId, x.SourceInventoryCostLayerId }).IsUnique();
        entity.HasOne(x => x.InventoryTransferDetail).WithMany().HasForeignKey(x => x.InventoryTransferDetailId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.SourceInventoryCostLayer).WithMany().HasForeignKey(x => x.SourceInventoryCostLayerId).OnDelete(DeleteBehavior.Restrict);
    }
}
