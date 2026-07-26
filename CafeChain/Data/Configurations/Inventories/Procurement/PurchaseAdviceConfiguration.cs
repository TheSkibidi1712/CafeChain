using CafeChain.Models.Inventories.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Procurement
{
    public sealed class PurchaseAdviceConfiguration : IEntityTypeConfiguration<PurchaseAdvice>
    {
        public void Configure(EntityTypeBuilder<PurchaseAdvice> b)
        {
            b.ToTable("PurchaseAdvices");
            b.HasKey(x => x.PurchaseAdviceId);
            b.Property(x => x.AdviceNumber).HasMaxLength(40).IsRequired();
            b.Property(x => x.RequestKey).HasMaxLength(64).IsRequired();
            b.Property(x => x.Status).HasMaxLength(30).IsRequired();
            b.Property(x => x.Priority).HasMaxLength(20).IsRequired();
            b.Property(x => x.Note).HasMaxLength(1000);
            b.Property(x => x.RejectionReason).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => x.AdviceNumber).IsUnique();
            b.HasIndex(x => x.RequestKey).IsUnique();
            b.HasIndex(x => new { x.StoreId, x.Status, x.CreatedAtUtc });
            b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.RequestedByStaff).WithMany().HasForeignKey(x => x.RequestedByStaffId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ReviewedByStaff).WithMany().HasForeignKey(x => x.ReviewedByStaffId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.RejectedByStaff).WithMany().HasForeignKey(x => x.RejectedByStaffId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.CancelledByStaff).WithMany().HasForeignKey(x => x.CancelledByStaffId).OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class PurchaseAdviceLineConfiguration : IEntityTypeConfiguration<PurchaseAdviceLine>
    {
        public void Configure(EntityTypeBuilder<PurchaseAdviceLine> b)
        {
            b.ToTable("PurchaseAdviceLines", table =>
            {
                table.HasCheckConstraint("CK_PurchaseAdviceLines_RequestedPositive", "[RequestedPurchaseBaseQuantity] > 0");
                table.HasCheckConstraint("CK_PurchaseAdviceLines_ProcurementRequestedPositive", "[RequestedProcurementQuantity] IS NULL OR [RequestedProcurementQuantity] > 0");
                table.HasCheckConstraint("CK_PurchaseAdviceLines_AllocatedNonNegative", "[AllocatedToPoBaseQuantity] >= 0");
                table.HasCheckConstraint("CK_PurchaseAdviceLines_AcceptedNonNegative", "[AcceptedBaseQuantity] >= 0");
                table.HasCheckConstraint("CK_PurchaseAdviceLines_ClosedNonNegative", "[ClosedBaseQuantity] >= 0");
            });
            b.HasKey(x => x.PurchaseAdviceLineId);
            b.Property(x => x.RequestedPurchaseBaseQuantity).HasPrecision(18, 3);
            b.Property(x => x.AllocatedToPoBaseQuantity).HasPrecision(18, 3).HasDefaultValue(0m);
            b.Property(x => x.AcceptedBaseQuantity).HasPrecision(18, 3).HasDefaultValue(0m);
            b.Property(x => x.ClosedBaseQuantity).HasPrecision(18, 3).HasDefaultValue(0m);
            b.Property(x => x.RequestedProcurementQuantity).HasPrecision(18, 3);
            b.Property(x => x.AllocatedToPoProcurementQuantity).HasPrecision(18, 3).HasDefaultValue(0m);
            b.Property(x => x.AcceptedProcurementQuantity).HasPrecision(18, 3).HasDefaultValue(0m);
            b.Property(x => x.ClosedProcurementQuantity).HasPrecision(18, 3).HasDefaultValue(0m);
            b.Property(x => x.Note).HasMaxLength(500);
            b.Property(x => x.RowVersion).IsRowVersion();
            b.HasIndex(x => x.RestockRequestId);
            b.HasIndex(x => x.RestockRequestId)
                .IsUnique()
                .HasFilter("[IsActiveReservation] = 1")
                .HasDatabaseName("UX_PurchaseAdviceLines_ActiveRestock");
            b.HasOne(x => x.PurchaseAdvice).WithMany(x => x.Lines).HasForeignKey(x => x.PurchaseAdviceId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.RestockRequest).WithMany().HasForeignKey(x => x.RestockRequestId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.BaseUnit).WithMany().HasForeignKey(x => x.BaseUnitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ProcurementUnit).WithMany().HasForeignKey(x => x.ProcurementUnitId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.RestockSourcingAllocation).WithMany().HasForeignKey(x => x.RestockSourcingAllocationId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.ProcurementUnitId);
            b.HasIndex(x => x.RestockSourcingAllocationId);
        }
    }

    public sealed class PurchaseAdviceTransitionConfiguration : IEntityTypeConfiguration<PurchaseAdviceTransition>
    {
        public void Configure(EntityTypeBuilder<PurchaseAdviceTransition> b)
        {
            b.ToTable("PurchaseAdviceTransitions");
            b.HasKey(x => x.PurchaseAdviceTransitionId);
            b.Property(x => x.PreviousStatus).HasMaxLength(30);
            b.Property(x => x.NewStatus).HasMaxLength(30).IsRequired();
            b.Property(x => x.Reason).HasMaxLength(500);
            b.HasIndex(x => new { x.PurchaseAdviceId, x.OccurredAtUtc });
            b.HasOne(x => x.PurchaseAdvice).WithMany(x => x.Transitions).HasForeignKey(x => x.PurchaseAdviceId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.ActorStaff).WithMany().HasForeignKey(x => x.ActorStaffId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
