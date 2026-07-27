using CafeChain.Models.Inventories.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Stock;

public sealed class RestockSourcingAllocationConfiguration
    : IEntityTypeConfiguration<RestockSourcingAllocation>
{
    public void Configure(EntityTypeBuilder<RestockSourcingAllocation> entity)
    {
        entity.ToTable("RestockSourcingAllocations", table =>
        {
            table.HasCheckConstraint(
                "CK_RestockSourcingAllocations_Quantity",
                "[ProcurementQuantity] > 0");
            table.HasCheckConstraint(
                "CK_RestockSourcingAllocations_Decision",
                "[DecisionType] IN ('TRANSFER','PURCHASE','PRODUCTION','REJECT')");
            table.HasCheckConstraint(
                "CK_RestockSourcingAllocations_ActivePurchaseLink",
                "[Status] NOT IN ('ACTIVE','PENDING_PURCHASE') OR [DecisionType] <> 'PURCHASE' OR [PurchaseAdviceLineId] IS NOT NULL OR [PurchaseOrderLineId] IS NOT NULL OR [Status] = 'PENDING_PURCHASE'");
        });

        entity.HasKey(x => x.RestockSourcingAllocationId);
        entity.Property(x => x.DecisionType).HasMaxLength(24).IsRequired();
        entity.Property(x => x.ProcurementQuantity).HasPrecision(18, 3).IsRequired();
        entity.Property(x => x.Status).HasMaxLength(24).IsRequired();
        entity.Property(x => x.SourceDocumentType).HasMaxLength(64);
        entity.Property(x => x.Reason).HasMaxLength(500);
        entity.Property(x => x.ReleaseReason).HasMaxLength(500);
        entity.Property(x => x.CreatedAtUtc).IsRequired();
        entity.Property(x => x.RowVersion).IsRowVersion();

        entity.HasOne(x => x.RestockRequest)
            .WithMany(x => x.SourcingAllocations)
            .HasForeignKey(x => x.RestockRequestId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.ProcurementUnit)
            .WithMany()
            .HasForeignKey(x => x.ProcurementUnitId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.PurchaseAdviceLine)
            .WithMany()
            .HasForeignKey(x => x.PurchaseAdviceLineId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.PurchaseOrderLine)
            .WithMany()
            .HasForeignKey(x => x.PurchaseOrderLineId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.InventoryTransfer)
            .WithMany()
            .HasForeignKey(x => x.InventoryTransferId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.ProductionRun)
            .WithMany()
            .HasForeignKey(x => x.ProductionRunId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.CreatedByStaff)
            .WithMany()
            .HasForeignKey(x => x.CreatedByStaffId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.ReleasedByStaff)
            .WithMany()
            .HasForeignKey(x => x.ReleasedByStaffId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => new { x.RestockRequestId, x.Status });
        entity.HasIndex(x => new { x.RestockRequestId, x.DecisionType, x.Status });
        entity.HasIndex(x => x.PurchaseAdviceLineId)
            .IsUnique()
            .HasFilter("[PurchaseAdviceLineId] IS NOT NULL AND [Status] = 'ACTIVE'");
        entity.HasIndex(x => x.PurchaseOrderLineId)
            .IsUnique()
            .HasFilter("[PurchaseOrderLineId] IS NOT NULL AND [Status] = 'ACTIVE'");
        entity.HasIndex(x => new { x.RestockRequestId, x.SourceDocumentType, x.SourceDocumentId, x.SourceDocumentLineId })
            .IsUnique()
            .HasFilter("[SourceDocumentType] IS NOT NULL AND [SourceDocumentId] IS NOT NULL AND [Status] = 'ACTIVE'");
    }
}
