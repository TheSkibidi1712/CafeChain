using CafeChain.Models.Inventories.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Stock
{
    public class BranchReceiptLineConfiguration : IEntityTypeConfiguration<BranchReceiptLine>
    {
        public void Configure(EntityTypeBuilder<BranchReceiptLine> entity)
        {
            entity.ToTable("BranchReceiptLines", t =>
            {
                // Issue #128 / #122 identity truth table — no Recipe-only new lines.
                t.HasCheckConstraint(
                    "CK_BranchReceiptLines_Identity",
                    @"
(
  ([IngredientId] IS NOT NULL AND [RecipeId] IS NULL AND [PreparedItemId] IS NULL)
  OR ([IngredientId] IS NULL AND [RecipeId] IS NULL AND [PreparedItemId] IS NOT NULL)
  OR ([IngredientId] IS NULL AND [RecipeId] IS NOT NULL AND [PreparedItemId] IS NOT NULL)
)");
            });

            entity.HasKey(x => x.BranchReceiptLineId);

            entity.Property(x => x.InputQuantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.ReceivedBaseQuantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.ActualPackagePrice)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.PackageQuantitySnapshot)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.BaseUnitCostSnapshot)
                .HasColumnType("decimal(18,4)")
                .IsRequired();

            entity.Property(x => x.LineTotalCost)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(x => x.CreatedAt).IsRequired();

            entity.Property(x => x.RowVersion)
                .IsRowVersion();

            entity.HasOne(x => x.BranchReceipt)
                .WithMany(x => x.Lines)
                .HasForeignKey(x => x.BranchReceiptId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RestockRequest)
                .WithMany()
                .HasForeignKey(x => x.RestockRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SourceInventoryTransferDetail)
                .WithMany()
                .HasForeignKey(x => x.SourceInventoryTransferDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SourceTransferCostAllocation)
                .WithMany()
                .HasForeignKey(x => x.SourceTransferCostAllocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RestockRequestFulfillment)
                .WithMany()
                .HasForeignKey(x => x.RestockRequestFulfillmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Ingredient)
                .WithMany()
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PreparedItem)
                .WithMany()
                .HasForeignKey(x => x.PreparedItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Recipe)
                .WithMany()
                .HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.InputUnit)
                .WithMany()
                .HasForeignKey(x => x.InputUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.BaseUnit)
                .WithMany()
                .HasForeignKey(x => x.BaseUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Supplier)
                .WithMany()
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.IngredientSupplier)
                .WithMany()
                .HasForeignKey(x => x.IngredientSupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PackageUnitSnapshot)
                .WithMany()
                .HasForeignKey(x => x.PackageUnitIdSnapshot)
                .OnDelete(DeleteBehavior.Restrict);

            // Inverse of InventoryTransaction.BranchReceiptLineId is configured on transaction side.
            entity.HasOne(x => x.InventoryTransaction)
                .WithMany()
                .HasForeignKey(x => x.InventoryTransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.BranchReceiptId);
            entity.HasIndex(x => x.RestockRequestId);
            entity.HasIndex(x => new { x.BranchReceiptId, x.SourceTransferCostAllocationId })
                .IsUnique()
                .HasFilter("[SourceTransferCostAllocationId] IS NOT NULL");
            entity.HasIndex(x => x.IngredientId);
            entity.HasIndex(x => x.PreparedItemId);
        }
    }
}
