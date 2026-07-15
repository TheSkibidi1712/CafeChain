using CafeChain.Models.Inventories.Costing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Costing;

public sealed class InventoryNegativeCostGapConfiguration : IEntityTypeConfiguration<InventoryNegativeCostGap>
{
    public void Configure(EntityTypeBuilder<InventoryNegativeCostGap> entity)
    {
        entity.ToTable("InventoryNegativeCostGaps", table =>
        {
            table.HasCheckConstraint("CK_InventoryNegativeCostGap_Source", "[SourceType] IN ('POS_SALE','MANUAL_DOCUMENT','LEGACY_BALANCE')");
            table.HasCheckConstraint("CK_InventoryNegativeCostGap_Status", "[Status] IN ('OPEN','PARTIALLY_SETTLED','SETTLED','CANCELLED')");
            table.HasCheckConstraint("CK_InventoryNegativeCostGap_Identity", "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
            table.HasCheckConstraint("CK_InventoryNegativeCostGap_Quantity", "[OriginalQuantity] > 0 AND [OutstandingQuantity] >= 0 AND [OutstandingQuantity] <= [OriginalQuantity]");
        });
        entity.HasKey(x => x.InventoryNegativeCostGapId);
        entity.Property(x => x.SourceType).HasMaxLength(30).IsRequired();
        entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
        entity.Property(x => x.OriginalQuantity).HasColumnType("decimal(18,3)");
        entity.Property(x => x.OutstandingQuantity).HasColumnType("decimal(18,3)");
        entity.Property(x => x.RowVersion).IsRowVersion();
        entity.HasIndex(x => new { x.StoreInventoryId, x.Status, x.OccurredAt });
        entity.HasIndex(x => x.SalesCostGapId).IsUnique().HasFilter("[SalesCostGapId] IS NOT NULL");
        entity.HasIndex(x => x.InventoryDocumentDetailId).IsUnique().HasFilter("[InventoryDocumentDetailId] IS NOT NULL");
        entity.HasOne<CafeChain.Models.Stores.StoreInventory>().WithMany().HasForeignKey(x => x.StoreInventoryId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<SalesCostGap>().WithMany().HasForeignKey(x => x.SalesCostGapId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<CafeChain.Models.Inventories.Documents.InventoryDocumentDetail>().WithMany().HasForeignKey(x => x.InventoryDocumentDetailId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.InventoryTransaction).WithMany().HasForeignKey(x => x.InventoryTransactionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<CafeChain.Models.Inventories.Approvals.InventoryNegativeApproval>().WithMany().HasForeignKey(x => x.InventoryNegativeApprovalId).OnDelete(DeleteBehavior.Restrict);
    }
}
