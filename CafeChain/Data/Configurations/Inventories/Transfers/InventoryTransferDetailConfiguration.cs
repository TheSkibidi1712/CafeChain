using CafeChain.Models.Inventories.Transfers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Transfers
{
    public class InventoryTransferDetailConfiguration : IEntityTypeConfiguration<InventoryTransferDetail>
    {
        public void Configure(EntityTypeBuilder<InventoryTransferDetail> entity)
        {
            entity.ToTable("InventoryTransferDetails", t =>
            {
                t.HasCheckConstraint(
                    "CK_InventoryTransferDetail_ExportQuantity",
                    "[ExportQuantity] > 0"
                );

                t.HasCheckConstraint(
                    "CK_InventoryTransferDetail_ReceivedQuantity",
                    "[ReceivedQuantity] >= 0"
                );

                t.HasCheckConstraint(
                    "CK_InventoryTransferDetail_Received_NotGreater_Export",
                    "[ReceivedQuantity] <= [ExportQuantity]"
                );

                t.HasCheckConstraint(
                    "CK_InventoryTransferDetail_UnitPrice",
                    "[UnitPrice] IS NULL OR [UnitPrice] >= 0"
                );
            });

            entity.HasKey(x => x.InventoryTransferDetailId);

            // ================= PROPERTY =================

            entity.Property(x => x.ExportQuantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.ReceivedQuantity)
                .HasColumnType("decimal(18,3)")
                .HasDefaultValue(0);

            entity.Property(x => x.UnitPrice)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.Note)
                .HasMaxLength(500);

            // ================= RELATION =================

            entity.HasOne(x => x.InventoryTransfer)
                .WithMany(x => x.Details)
                .HasForeignKey(x => x.InventoryTransferId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Ingredient)
                .WithMany(i => i.InventoryTransferDetails)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            entity.HasIndex(x => new
            {
                x.InventoryTransferId,
                x.IngredientId
            }).IsUnique();

            entity.HasIndex(x => x.IngredientId);

            entity.HasIndex(x => new
            {
                x.IngredientId,
                x.InventoryTransferId
            });
        }
    }
}
