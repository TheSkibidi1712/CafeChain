using CafeChain.Models.Inventories.Costing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Costing;

public sealed class InventoryCostGapSettlementConfiguration : IEntityTypeConfiguration<InventoryCostGapSettlement>
{
    public void Configure(EntityTypeBuilder<InventoryCostGapSettlement> entity)
    {
        entity.ToTable("InventoryCostGapSettlements", table => table.HasCheckConstraint(
            "CK_InventoryCostGapSettlement_Quantity", "[Quantity] > 0 AND [UnitCost] >= 0 AND [TotalCost] >= 0"));
        entity.HasKey(x => x.InventoryCostGapSettlementId);
        entity.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
        entity.Property(x => x.UnitCost).HasColumnType("decimal(18,6)");
        entity.Property(x => x.TotalCost).HasColumnType("decimal(18,2)");
        entity.HasIndex(x => new { x.InventoryNegativeCostGapId, x.InboundInventoryCostLayerId }).IsUnique();
        entity.HasOne(x => x.InventoryNegativeCostGap).WithMany(x => x.Settlements).HasForeignKey(x => x.InventoryNegativeCostGapId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.InboundInventoryCostLayer).WithMany().HasForeignKey(x => x.InboundInventoryCostLayerId).OnDelete(DeleteBehavior.Restrict);
    }
}
