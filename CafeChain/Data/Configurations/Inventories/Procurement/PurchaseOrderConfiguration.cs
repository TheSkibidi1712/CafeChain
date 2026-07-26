using CafeChain.Models.Inventories.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Procurement
{
    public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
    {
        public void Configure(EntityTypeBuilder<PurchaseOrder> b)
        {
            b.ToTable("PurchaseOrders");
            b.HasKey(x => x.PurchaseOrderId);
            b.Property(x => x.Code).HasMaxLength(40).IsRequired();
            b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.Status).HasMaxLength(30).IsRequired();
            b.Property(x => x.Note).HasMaxLength(1000);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => new { x.StoreId, x.Status });
            b.HasIndex(x => x.PurchaseOrderBatchId);
            b.HasIndex(x => x.MasterPurchaseOrderId);
            b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.CreatedByStaff).WithMany().HasForeignKey(x => x.CreatedByStaffId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ApprovedByStaff).WithMany().HasForeignKey(x => x.ApprovedByStaffId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.SentByStaff).WithMany().HasForeignKey(x => x.SentByStaffId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.PurchaseOrderBatch).WithMany(x => x.ChildPurchaseOrders).HasForeignKey(x => x.PurchaseOrderBatchId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.MasterPurchaseOrder).WithMany(x => x.ChildPurchaseOrders).HasForeignKey(x => x.MasterPurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
    {
        public void Configure(EntityTypeBuilder<PurchaseOrderLine> b)
        {
            b.ToTable("PurchaseOrderLines", table =>
                table.HasCheckConstraint(
                    "CK_PurchaseOrderLines_ClosedRemainingQuantity_NonNegative",
                    "[ClosedRemainingQuantity] >= 0"));
            b.HasKey(x => x.PurchaseOrderLineId);
            b.Property(x => x.PackageQuantitySnapshot).HasPrecision(18, 3);
            b.Property(x => x.PackagePriceSnapshot).HasPrecision(18, 2);
            b.Property(x => x.PackageCount).HasPrecision(18, 3);
            b.Property(x => x.OrderedBaseQuantity).HasPrecision(18, 3);
            b.Property(x => x.OrderedPackQuantity).HasPrecision(18, 3);
            b.Property(x => x.PackSizeProcurementQuantity).HasPrecision(18, 3);
            b.Property(x => x.OrderedProcurementQuantity).HasPrecision(18, 3);
            b.Property(x => x.RoundingSurplusProcurementQuantity).HasPrecision(18, 3);
            b.Property(x => x.AcceptedPackQuantity).HasPrecision(18, 3);
            b.Property(x => x.AcceptedProcurementQuantity).HasPrecision(18, 3);
            b.Property(x => x.ClosedProcurementQuantity).HasPrecision(18, 3).HasDefaultValue(0m);
            b.Property(x => x.InventoryPostingBaseQuantity).HasPrecision(18, 3);
            b.Property(x => x.ProcurementToInventoryFactor).HasPrecision(18, 6);
            b.Property(x => x.ClosedRemainingQuantity).HasPrecision(18, 3).HasDefaultValue(0m);
            b.Property(x => x.CloseRemainingReason).HasMaxLength(500);
            b.Property(x => x.Note).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => x.RestockRequestId);
            b.HasIndex(x => x.PurchaseAdviceLineId);
            b.HasIndex(x => x.ProcurementUnitId);
            b.HasIndex(x => x.InventoryBaseUnitId);
            b.HasOne(x => x.PurchaseOrder).WithMany(x => x.Lines).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.RestockRequest).WithMany().HasForeignKey(x => x.RestockRequestId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.PurchaseAdviceLine).WithMany().HasForeignKey(x => x.PurchaseAdviceLineId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.IngredientSupplier).WithMany().HasForeignKey(x => x.IngredientSupplierId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.PackageUnitSnapshot).WithMany().HasForeignKey(x => x.PackageUnitIdSnapshot).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ProcurementUnit).WithMany().HasForeignKey(x => x.ProcurementUnitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.InventoryBaseUnit).WithMany().HasForeignKey(x => x.InventoryBaseUnitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ClosedRemainingByStaff).WithMany().HasForeignKey(x => x.ClosedRemainingByStaffId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.ClosedRemainingByStaffId);
        }
    }

    public sealed class PurchaseOrderReceiptPostingConfiguration : IEntityTypeConfiguration<PurchaseOrderReceiptPosting>
    {
        public void Configure(EntityTypeBuilder<PurchaseOrderReceiptPosting> b)
        {
            b.ToTable("PurchaseOrderReceiptPostings");
            b.HasKey(x => x.PurchaseOrderReceiptPostingId);
            b.Property(x => x.AcceptedBaseQuantity).HasPrecision(18, 3);
            b.Property(x => x.RejectedBaseQuantity).HasPrecision(18, 3);
            b.Property(x => x.AcceptedProcurementQuantity).HasPrecision(18, 3);
            b.Property(x => x.RejectedProcurementQuantity).HasPrecision(18, 3);
            b.Property(x => x.InventoryPostingBaseQuantity).HasPrecision(18, 3);
            b.Property(x => x.ProcurementToInventoryFactor).HasPrecision(18, 6);
            b.HasIndex(x => x.BranchReceiptLineId).IsUnique();
            b.HasIndex(x => x.PurchaseOrderLineId);
            b.HasIndex(x => x.ProcurementUnitId);
            b.HasIndex(x => x.InventoryBaseUnitId);
            b.HasOne(x => x.PurchaseOrderLine).WithMany(x => x.ReceiptPostings).HasForeignKey(x => x.PurchaseOrderLineId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.BranchReceiptLine).WithMany().HasForeignKey(x => x.BranchReceiptLineId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.CreatedByStaff).WithMany().HasForeignKey(x => x.CreatedByStaffId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ProcurementUnit).WithMany().HasForeignKey(x => x.ProcurementUnitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.InventoryBaseUnit).WithMany().HasForeignKey(x => x.InventoryBaseUnitId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
