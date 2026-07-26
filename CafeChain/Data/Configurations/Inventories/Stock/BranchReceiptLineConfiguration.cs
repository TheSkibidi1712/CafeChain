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
                t.HasCheckConstraint(
                    "CK_BranchReceiptLines_Quantities",
                    "[InputQuantity] > 0 AND [ReceivedBaseQuantity] >= 0 AND [RejectedBaseQuantity] >= 0 AND ([ReceivedBaseQuantity] + [RejectedBaseQuantity]) > 0");
                t.HasCheckConstraint(
                    "CK_BranchReceiptLines_RejectionReason",
                    "[RejectedBaseQuantity] = 0 OR (LEN(LTRIM(RTRIM([RejectionReason]))) > 0 AND LEN(LTRIM(RTRIM([RejectionIssueType]))) > 0)");
            });

            entity.HasKey(x => x.BranchReceiptLineId);

            entity.Property(x => x.InputQuantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.ReceivedBaseQuantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.RejectedBaseQuantity)
                .HasColumnType("decimal(18,3)")
                .HasDefaultValue(0m)
                .IsRequired();
            entity.Property(x => x.ReceivedPackQuantity).HasPrecision(18, 3);
            entity.Property(x => x.AcceptedPackQuantity).HasPrecision(18, 3);
            entity.Property(x => x.ReceivedProcurementQuantity).HasPrecision(18, 3);
            entity.Property(x => x.RejectedProcurementQuantity).HasPrecision(18, 3);
            entity.Property(x => x.AcceptedProcurementQuantity).HasPrecision(18, 3);
            entity.Property(x => x.InventoryPostingBaseQuantity).HasPrecision(18, 3);
            entity.Property(x => x.ProcurementToInventoryFactor).HasPrecision(18, 6);

            entity.Property(x => x.RejectionReason).HasMaxLength(500);
            entity.Property(x => x.RejectionIssueType).HasMaxLength(40);

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

            entity.HasOne(x => x.PurchaseOrderLine)
                .WithMany()
                .HasForeignKey(x => x.PurchaseOrderLineId)
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
            entity.HasOne(x => x.ProcurementUnit)
                .WithMany()
                .HasForeignKey(x => x.ProcurementUnitId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.InventoryBaseUnit)
                .WithMany()
                .HasForeignKey(x => x.InventoryBaseUnitId)
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
            entity.HasIndex(x => x.PurchaseOrderLineId);
            entity.HasIndex(x => new { x.BranchReceiptId, x.SourceTransferCostAllocationId })
                .IsUnique()
                .HasFilter("[SourceTransferCostAllocationId] IS NOT NULL");
            entity.HasIndex(x => x.IngredientId);
            entity.HasIndex(x => x.PreparedItemId);
            entity.HasIndex(x => x.ProcurementUnitId);
            entity.HasIndex(x => x.InventoryBaseUnitId);
        }
    }
}
