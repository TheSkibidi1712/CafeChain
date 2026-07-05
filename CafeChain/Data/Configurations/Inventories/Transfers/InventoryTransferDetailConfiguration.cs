using CafeChain.Models.Inventories.Transfers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Transfers
{
    public class InventoryTransferDetailConfiguration : IEntityTypeConfiguration<InventoryTransferDetail>
    {
        public void Configure(EntityTypeBuilder<InventoryTransferDetail> entity)
        {
            entity.ToTable("InventoryTransferDetails", table =>
            {
                table.HasCheckConstraint(
                    "CK_InventoryTransferDetail_Quantity",
                    "[Quantity] > 0");

                table.HasCheckConstraint(
                    "CK_InventoryTransferDetail_BaseQuantity",
                    "[BaseQuantity] > 0");

                table.HasCheckConstraint(
                    "CK_InventoryTransferDetail_UnitPrice",
                    "[UnitPrice] IS NULL OR [UnitPrice] >= 0");
            });

            entity.HasKey(x => x.InventoryTransferDetailId);

            entity.Property(x => x.Quantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.BaseQuantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.SourceBeforeQty)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.SourceAfterQty)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.DestinationBeforeQty)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.DestinationAfterQty)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.UnitPrice)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.Note)
                .HasMaxLength(500);

            entity.HasOne(x => x.InventoryTransfer)
                .WithMany(x => x.Details)
                .HasForeignKey(x => x.InventoryTransferId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Ingredient)
                .WithMany(x => x.InventoryTransferDetails)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Unit)
                .WithMany()
                .HasForeignKey(x => x.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.InventoryTransferId, x.IngredientId })
                .IsUnique();

            entity.HasIndex(x => x.IngredientId);
            entity.HasIndex(x => x.UnitId);
            entity.HasIndex(x => new { x.IngredientId, x.InventoryTransferId });
        }
    }
}
