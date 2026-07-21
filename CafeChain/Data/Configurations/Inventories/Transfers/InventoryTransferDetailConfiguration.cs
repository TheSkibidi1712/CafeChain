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

                table.HasCheckConstraint(
                    "CK_InventoryTransferDetail_ExactlyOneIdentity",
                    "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
            });

            entity.HasKey(x => x.InventoryTransferDetailId);

            entity.Property(x => x.Quantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.BaseQuantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.DispatchedBaseQuantity).HasColumnType("decimal(18,3)");
            entity.Property(x => x.ReceivedBaseQuantity).HasColumnType("decimal(18,3)");

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

            entity.HasOne(x => x.PreparedItem)
                .WithMany()
                .HasForeignKey(x => x.PreparedItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RestockRequest)
                .WithMany()
                .HasForeignKey(x => x.RestockRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RestockRequestFulfillment)
                .WithMany()
                .HasForeignKey(x => x.RestockRequestFulfillmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Unit)
                .WithMany()
                .HasForeignKey(x => x.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ParentInventoryTransferDetail)
                .WithMany(x => x.FollowUpDetails)
                .HasForeignKey(x => x.ParentInventoryTransferDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.InventoryTransferId, x.IngredientId })
                .IsUnique()
                .HasFilter("[IngredientId] IS NOT NULL");

            entity.HasIndex(x => new { x.InventoryTransferId, x.PreparedItemId })
                .IsUnique()
                .HasFilter("[PreparedItemId] IS NOT NULL");

            entity.HasIndex(x => x.IngredientId);
            entity.HasIndex(x => x.PreparedItemId);
            entity.HasIndex(x => x.RestockRequestId);
            entity.HasIndex(x => x.RestockRequestFulfillmentId);
            entity.HasIndex(x => x.UnitId);
            entity.HasIndex(x => new { x.IngredientId, x.InventoryTransferId });
            entity.HasIndex(x => x.ParentInventoryTransferDetailId);
        }
    }
}
