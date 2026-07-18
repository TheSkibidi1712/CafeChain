using CafeChain.Models.Inventories.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Procurement;

public sealed class PurchaseOrderBatchConfiguration : IEntityTypeConfiguration<PurchaseOrderBatch>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderBatch> b)
    {
        b.ToTable("PurchaseOrderBatches", table =>
            table.HasCheckConstraint("CK_PurchaseOrderBatches_DeliveryWindow", "[ExpectedDeliveryTo] >= [ExpectedDeliveryFrom]"));
        b.HasKey(x => x.PurchaseOrderBatchId);
        b.Property(x => x.BatchNumber).HasMaxLength(40).IsRequired();
        b.Property(x => x.RequestKey).HasMaxLength(64).IsRequired();
        b.Property(x => x.Status).HasMaxLength(30).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.CancellationReason).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.BatchNumber).IsUnique();
        b.HasIndex(x => x.RequestKey).IsUnique();
        b.HasIndex(x => new { x.SupplierId, x.Status, x.CreatedAtUtc });
        b.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CreatedByStaff).WithMany().HasForeignKey(x => x.CreatedByStaffId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ApprovedByStaff).WithMany().HasForeignKey(x => x.ApprovedByStaffId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.CancelledByStaff).WithMany().HasForeignKey(x => x.CancelledByStaffId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PurchaseOrderBatchLineConfiguration : IEntityTypeConfiguration<PurchaseOrderBatchLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderBatchLine> b)
    {
        b.ToTable("PurchaseOrderBatchLines", table =>
        {
            table.HasCheckConstraint("CK_PurchaseOrderBatchLines_PackagePositive", "[PackageQuantitySnapshot] > 0 AND [TotalPackageCount] > 0");
            table.HasCheckConstraint("CK_PurchaseOrderBatchLines_BasePositive", "[TotalBaseQuantity] > 0");
            table.HasCheckConstraint("CK_PurchaseOrderBatchLines_PriceNonNegative", "[PackagePriceSnapshot] >= 0 AND [LineTotal] >= 0");
        });
        b.HasKey(x => x.PurchaseOrderBatchLineId);
        b.Property(x => x.PackageQuantitySnapshot).HasPrecision(18, 5);
        b.Property(x => x.TotalPackageCount).HasPrecision(18, 3);
        b.Property(x => x.TotalBaseQuantity).HasPrecision(18, 3);
        b.Property(x => x.PackagePriceSnapshot).HasPrecision(18, 2);
        b.Property(x => x.LineTotal).HasPrecision(18, 2);
        b.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasIndex(x => new { x.PurchaseOrderBatchId, x.IngredientId });
        b.HasOne(x => x.PurchaseOrderBatch).WithMany(x => x.Lines).HasForeignKey(x => x.PurchaseOrderBatchId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.IngredientSupplier).WithMany().HasForeignKey(x => x.IngredientSupplierId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PackageUnit).WithMany().HasForeignKey(x => x.PackageUnitId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PurchaseOrderLineAllocationConfiguration : IEntityTypeConfiguration<PurchaseOrderLineAllocation>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLineAllocation> b)
    {
        b.ToTable("PurchaseOrderLineAllocations", table =>
        {
            table.HasCheckConstraint("CK_PurchaseOrderLineAllocations_BasePositive", "[AllocatedBaseQuantity] > 0");
            table.HasCheckConstraint("CK_PurchaseOrderLineAllocations_PackagePositive", "[AllocatedPackageQuantity] > 0");
        });
        b.HasKey(x => x.PurchaseOrderLineAllocationId);
        b.Property(x => x.AllocatedBaseQuantity).HasPrecision(18, 3);
        b.Property(x => x.AllocatedPackageQuantity).HasPrecision(18, 3);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.PurchaseAdviceLineId);
        b.HasIndex(x => x.PurchaseOrderBatchLineId);
        b.HasIndex(x => x.PurchaseOrderId);
        b.HasIndex(x => x.PurchaseOrderLineId).IsUnique();
        b.HasOne(x => x.PurchaseAdviceLine).WithMany().HasForeignKey(x => x.PurchaseAdviceLineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PurchaseOrderBatchLine).WithMany(x => x.Allocations).HasForeignKey(x => x.PurchaseOrderBatchLineId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.PurchaseOrder).WithMany(x => x.BatchAllocations).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PurchaseOrderLine).WithMany(x => x.BatchAllocations).HasForeignKey(x => x.PurchaseOrderLineId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PurchaseOrderBatchDocumentRevisionConfiguration : IEntityTypeConfiguration<PurchaseOrderBatchDocumentRevision>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderBatchDocumentRevision> b)
    {
        b.ToTable("PurchaseOrderBatchDocumentRevisions", table =>
        {
            table.HasCheckConstraint("CK_PurchaseOrderBatchDocumentRevisions_RevisionPositive", "[RevisionNumber] > 0");
            table.HasCheckConstraint(
                "CK_PurchaseOrderBatchDocumentRevisions_Status",
                "[Status] IN ('GENERATED','SENT','SUPERSEDED')");
        });
        b.HasKey(x => x.PurchaseOrderBatchDocumentRevisionId);
        b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.StorageReference).HasMaxLength(500).IsRequired();
        b.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.SnapshotJson).IsRequired();
        b.Property(x => x.Status).HasMaxLength(24).IsRequired();
        b.Property(x => x.SentChannel).HasMaxLength(32);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.PurchaseOrderBatchId, x.RevisionNumber }).IsUnique();
        b.HasIndex(x => new { x.PurchaseOrderBatchId, x.ContentHash }).IsUnique();
        b.HasIndex(x => new { x.PurchaseOrderBatchId, x.Status });
        b.HasOne(x => x.PurchaseOrderBatch)
            .WithMany(x => x.DocumentRevisions)
            .HasForeignKey(x => x.PurchaseOrderBatchId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.GeneratedByStaff)
            .WithMany()
            .HasForeignKey(x => x.GeneratedByStaffId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SentByStaff)
            .WithMany()
            .HasForeignKey(x => x.SentByStaffId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SupersededByRevision)
            .WithMany()
            .HasForeignKey(x => x.SupersededByRevisionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
