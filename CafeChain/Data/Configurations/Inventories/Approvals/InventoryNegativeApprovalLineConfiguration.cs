using CafeChain.Models.Inventories.Approvals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Approvals;

public sealed class InventoryNegativeApprovalLineConfiguration : IEntityTypeConfiguration<InventoryNegativeApprovalLine>
{
    public void Configure(EntityTypeBuilder<InventoryNegativeApprovalLine> entity)
    {
        entity.ToTable("InventoryNegativeApprovalLines", table =>
        {
            table.HasCheckConstraint("CK_InventoryNegativeApprovalLine_Identity", "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
            table.HasCheckConstraint("CK_InventoryNegativeApprovalLine_Quantity", "[IssueQty] > 0 AND [EffectiveMaxNegativeQty] >= 0");
        });
        entity.HasKey(x => x.InventoryNegativeApprovalLineId);
        entity.Property(x => x.BeforeQty).HasColumnType("decimal(18,3)");
        entity.Property(x => x.IssueQty).HasColumnType("decimal(18,3)");
        entity.Property(x => x.ProjectedAfterQty).HasColumnType("decimal(18,3)");
        entity.Property(x => x.EffectiveMaxNegativeQty).HasColumnType("decimal(18,3)");
        entity.Property(x => x.InventoryRowVersion).IsRequired();
        entity.HasIndex(x => new { x.InventoryNegativeApprovalId, x.InventoryDocumentDetailId }).IsUnique();
        entity.HasOne(x => x.InventoryNegativeApproval).WithMany(x => x.Lines).HasForeignKey(x => x.InventoryNegativeApprovalId).OnDelete(DeleteBehavior.Cascade);
    }
}
