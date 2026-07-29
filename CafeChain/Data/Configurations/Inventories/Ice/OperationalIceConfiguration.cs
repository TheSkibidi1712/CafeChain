using CafeChain.Application.Constants;
using CafeChain.Models.Inventories.Ice;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Ice;

public sealed class OperationalShiftConfiguration : IEntityTypeConfiguration<OperationalShift>
{
    public void Configure(EntityTypeBuilder<OperationalShift> entity)
    {
        entity.ToTable("OperationalShifts", table =>
        {
            table.HasCheckConstraint("CK_OperationalShifts_TimeRange", "[EndAtUtc] > [StartAtUtc]");
            table.HasCheckConstraint(
                "CK_OperationalShifts_Status",
                "[Status] IN ('Draft','Open','PendingApproval','ReconciliationRequired','Closed','Cancelled')");
            table.HasCheckConstraint(
                "CK_OperationalShifts_CreationSource",
                "([CreationSource] = 'Manual' AND [SourceScheduleShiftId] IS NULL) OR ([CreationSource] = 'StaffSchedule' AND [SourceScheduleShiftId] IS NOT NULL)");
        });
        entity.HasKey(x => x.OperationalShiftId);
        entity.Property(x => x.BusinessDate).HasColumnType("date");
        entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
        entity.Property(x => x.CreationSource).HasMaxLength(20).HasDefaultValue(OperationalIceCreationSources.Manual).IsRequired();
        entity.Property(x => x.Status).HasMaxLength(30).HasDefaultValue(OperationalIceStatuses.Draft).IsRequired();
        entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.RowVersion).IsRowVersion();
        entity.HasIndex(x => new { x.StoreId, x.BusinessDate, x.Name })
            .IsUnique()
            .HasFilter("[CreationSource] = 'Manual' AND [Status] <> 'Cancelled'");
        entity.HasIndex(x => new { x.StoreId, x.BusinessDate, x.Status });
        entity.HasIndex(x => new { x.StoreId, x.BusinessDate, x.CreationSource });
        entity.HasIndex(x => new { x.StoreId, x.BusinessDate, x.SourceScheduleShiftId })
            .IsUnique()
            .HasFilter("[SourceScheduleShiftId] IS NOT NULL AND [Status] <> 'Cancelled'");
        entity.HasIndex(x => x.ShiftLeadId);

        entity.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.SourceScheduleShift).WithMany().HasForeignKey(x => x.SourceScheduleShiftId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.ShiftLead).WithMany().HasForeignKey(x => x.ShiftLeadId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.CreatedByStaff).WithMany().HasForeignKey(x => x.CreatedByStaffId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.OpenedByStaff).WithMany().HasForeignKey(x => x.OpenedByStaffId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.ClosedByStaff).WithMany().HasForeignKey(x => x.ClosedByStaffId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OperationalShiftWorkShiftConfiguration : IEntityTypeConfiguration<OperationalShiftWorkShift>
{
    public void Configure(EntityTypeBuilder<OperationalShiftWorkShift> entity)
    {
        entity.ToTable("OperationalShiftWorkShifts");
        entity.HasKey(x => new { x.OperationalShiftId, x.WorkShiftId });
        entity.Property(x => x.LinkedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(x => x.WorkShiftId).IsUnique();
        entity.HasOne(x => x.OperationalShift).WithMany(x => x.WorkShiftLinks).HasForeignKey(x => x.OperationalShiftId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.WorkShift).WithMany().HasForeignKey(x => x.WorkShiftId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.LinkedByStaff).WithMany().HasForeignKey(x => x.LinkedByStaffId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class IcePolicyConfiguration : IEntityTypeConfiguration<IcePolicy>
{
    public void Configure(EntityTypeBuilder<IcePolicy> entity)
    {
        entity.ToTable("IcePolicies", table =>
        {
            table.HasCheckConstraint("CK_IcePolicies_SuggestedQuantity", "[SuggestedDailyQuantity] >= 0 AND [SuggestedShiftQuantity] >= 0");
            table.HasCheckConstraint("CK_IcePolicies_VarianceThreshold", "[VarianceApprovalQuantityThreshold] >= 0 AND [VarianceApprovalPercentThreshold] >= 0");
        });
        entity.HasKey(x => x.IcePolicyId);
        DecimalQuantity(entity.Property(x => x.SuggestedDailyQuantity));
        DecimalQuantity(entity.Property(x => x.SuggestedShiftQuantity));
        DecimalQuantity(entity.Property(x => x.VarianceApprovalQuantityThreshold));
        entity.Property(x => x.VarianceApprovalPercentThreshold).HasPrecision(9, 4);
        entity.Property(x => x.AllowSupplementalIssue).HasDefaultValue(true);
        entity.Property(x => x.AllowSameDayCarryOver).HasDefaultValue(true);
        entity.Property(x => x.RequireVarianceApproval).HasDefaultValue(true);
        entity.Property(x => x.Active).HasDefaultValue(true);
        entity.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.RowVersion).IsRowVersion();
        entity.HasIndex(x => x.StoreId).IsUnique();
        entity.HasIndex(x => new { x.StoreId, x.IngredientId }).IsUnique();
        entity.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.DisplayUnit).WithMany().HasForeignKey(x => x.DisplayUnitId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.UpdatedByStaff).WithMany().HasForeignKey(x => x.UpdatedByStaffId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void DecimalQuantity(PropertyBuilder<decimal> property) => property.HasPrecision(18, 3);
}

public sealed class IceAllocationConfiguration : IEntityTypeConfiguration<IceAllocation>
{
    public void Configure(EntityTypeBuilder<IceAllocation> entity)
    {
        entity.ToTable("IceAllocations", table =>
        {
            table.HasCheckConstraint("CK_IceAllocations_NonNegativeQuantities", "[OpeningCarryQuantity] >= 0 AND [InitialIssuedQuantity] >= 0 AND [SupplementalIssuedQuantity] >= 0 AND [ReturnedQuantity] >= 0 AND [ClosingCarryQuantity] >= 0 AND [TheoreticalUsageQuantity] >= 0 AND [ReservedOutstandingQuantity] >= 0");
            table.HasCheckConstraint("CK_IceAllocations_Status", "[Status] IN ('Draft','Open','PendingApproval','ReconciliationRequired','Closed','Cancelled')");
            table.HasCheckConstraint("CK_IceAllocations_CostStatus", "[CostSnapshotStatus] IN ('Available','Missing')");
            table.HasCheckConstraint("CK_IceAllocations_ReturnAudit", "([ReturnedQuantity] = 0) OR ([ReturnedByStaffId] IS NOT NULL AND [ReturnReceivedByStaffId] IS NOT NULL AND [ReturnedAtUtc] IS NOT NULL AND LEN(LTRIM(RTRIM([ReturnCondition]))) > 0)");
        });
        entity.HasKey(x => x.IceAllocationId);
        entity.Property(x => x.PublicId).HasDefaultValueSql("NEWSEQUENTIALID()");
        Quantity(entity.Property(x => x.OpeningCarryQuantity));
        Quantity(entity.Property(x => x.InitialIssuedQuantity));
        Quantity(entity.Property(x => x.SupplementalIssuedQuantity));
        Quantity(entity.Property(x => x.ReturnedQuantity));
        Quantity(entity.Property(x => x.ClosingCarryQuantity));
        Quantity(entity.Property(x => x.TheoreticalUsageQuantity));
        entity.Property(x => x.ActualUsageQuantity).HasPrecision(18, 3);
        entity.Property(x => x.VarianceQuantity).HasPrecision(18, 3);
        Quantity(entity.Property(x => x.ReservedOutstandingQuantity));
        entity.Property(x => x.UnitCostSnapshot).HasPrecision(18, 6);
        entity.Property(x => x.CostSnapshotStatus).HasMaxLength(20).HasDefaultValue(IceCostSnapshotStatuses.Missing).IsRequired();
        entity.Property(x => x.ReservationReference).HasMaxLength(100).IsRequired();
        entity.Property(x => x.Status).HasMaxLength(30).HasDefaultValue(OperationalIceStatuses.Draft).IsRequired();
        entity.Property(x => x.ReconciliationReason).HasMaxLength(500);
        entity.Property(x => x.CloseReason).HasMaxLength(500);
        entity.Property(x => x.ReturnCondition).HasMaxLength(200);
        entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.Revision).HasDefaultValue(1);
        entity.Property(x => x.RowVersion).IsRowVersion();
        entity.HasIndex(x => x.PublicId).IsUnique();
        entity.HasIndex(x => x.ReservationReference).IsUnique();
        entity.HasIndex(x => new { x.OperationalShiftId, x.IngredientId }).IsUnique();
        entity.HasIndex(x => new { x.StoreInventoryId, x.Status });

        entity.HasOne(x => x.OperationalShift).WithMany(x => x.IceAllocations).HasForeignKey(x => x.OperationalShiftId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.IcePolicy).WithMany(x => x.Allocations).HasForeignKey(x => x.IcePolicyId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.StoreInventory).WithMany().HasForeignKey(x => x.StoreInventoryId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.CreatedByStaff).WithMany().HasForeignKey(x => x.CreatedByStaffId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.OpenedByStaff).WithMany().HasForeignKey(x => x.OpenedByStaffId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.ClosedByStaff).WithMany().HasForeignKey(x => x.ClosedByStaffId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.ReturnedByStaff).WithMany().HasForeignKey(x => x.ReturnedByStaffId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.ReturnReceivedByStaff).WithMany().HasForeignKey(x => x.ReturnReceivedByStaffId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void Quantity(PropertyBuilder<decimal> property) => property.HasPrecision(18, 3);
}

public sealed class IceSupplementalIssueConfiguration : IEntityTypeConfiguration<IceSupplementalIssue>
{
    public void Configure(EntityTypeBuilder<IceSupplementalIssue> entity)
    {
        entity.ToTable("IceSupplementalIssues", table =>
        {
            table.HasCheckConstraint("CK_IceSupplementalIssues_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_IceSupplementalIssues_Status", "[Status] IN ('Pending','Approved','Rejected','Cancelled')");
        });
        entity.HasKey(x => x.IceSupplementalIssueId);
        entity.Property(x => x.PublicId).HasDefaultValueSql("NEWSEQUENTIALID()");
        entity.Property(x => x.Quantity).HasPrecision(18, 3);
        entity.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        entity.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(IceSupplementalIssueStatuses.Pending).IsRequired();
        entity.Property(x => x.RejectionReason).HasMaxLength(500);
        entity.Property(x => x.RequestedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.RowVersion).IsRowVersion();
        entity.HasIndex(x => x.PublicId).IsUnique();
        entity.HasIndex(x => new { x.IceAllocationId, x.Status });
        entity.HasOne(x => x.IceAllocation).WithMany(x => x.SupplementalIssues).HasForeignKey(x => x.IceAllocationId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.RequestedByStaff).WithMany().HasForeignKey(x => x.RequestedByStaffId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.ApprovedByStaff).WithMany().HasForeignKey(x => x.ApprovedByStaffId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.RejectedByStaff).WithMany().HasForeignKey(x => x.RejectedByStaffId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class IceCarryOverConfiguration : IEntityTypeConfiguration<IceCarryOver>
{
    public void Configure(EntityTypeBuilder<IceCarryOver> entity)
    {
        entity.ToTable("IceCarryOvers", table =>
        {
            table.HasCheckConstraint("CK_IceCarryOvers_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_IceCarryOvers_DifferentShifts", "[FromOperationalShiftId] <> [ToOperationalShiftId]");
            table.HasCheckConstraint("CK_IceCarryOvers_Status", "[Status] IN ('Pending','Confirmed','Cancelled')");
        });
        entity.HasKey(x => x.IceCarryOverId);
        entity.Property(x => x.PublicId).HasDefaultValueSql("NEWSEQUENTIALID()");
        entity.Property(x => x.Quantity).HasPrecision(18, 3);
        entity.Property(x => x.Status).HasMaxLength(20).HasDefaultValue(IceCarryOverStatuses.Pending).IsRequired();
        entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.Property(x => x.RowVersion).IsRowVersion();
        entity.HasIndex(x => x.PublicId).IsUnique();
        entity.HasIndex(x => new { x.FromIceAllocationId, x.ToIceAllocationId }).IsUnique();
        entity.HasOne(x => x.FromOperationalShift).WithMany(x => x.OutgoingCarryOvers).HasForeignKey(x => x.FromOperationalShiftId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.ToOperationalShift).WithMany(x => x.IncomingCarryOvers).HasForeignKey(x => x.ToOperationalShiftId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.FromIceAllocation).WithMany(x => x.OutgoingCarryOvers).HasForeignKey(x => x.FromIceAllocationId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.ToIceAllocation).WithMany(x => x.IncomingCarryOvers).HasForeignKey(x => x.ToIceAllocationId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.HandedOverByStaff).WithMany().HasForeignKey(x => x.HandedOverByStaffId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.ReceivedByStaff).WithMany().HasForeignKey(x => x.ReceivedByStaffId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class IceInventoryPostingConfiguration : IEntityTypeConfiguration<IceInventoryPosting>
{
    public void Configure(EntityTypeBuilder<IceInventoryPosting> entity)
    {
        entity.ToTable("IceInventoryPostings", table =>
        {
            table.HasCheckConstraint("CK_IceInventoryPostings_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_IceInventoryPostings_Type", "[PostingType] IN ('VarianceOut')");
        });
        entity.HasKey(x => x.IceInventoryPostingId);
        entity.Property(x => x.PostingType).HasMaxLength(30).IsRequired();
        entity.Property(x => x.IdempotencyKey).HasMaxLength(150).IsRequired();
        entity.Property(x => x.Quantity).HasPrecision(18, 3);
        entity.Property(x => x.UnitCost).HasPrecision(18, 6);
        entity.Property(x => x.TotalCost).HasPrecision(18, 2);
        entity.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        entity.HasIndex(x => x.IdempotencyKey).IsUnique();
        entity.HasIndex(x => x.InventoryTransactionId).IsUnique().HasFilter("[InventoryTransactionId] IS NOT NULL");
        entity.HasIndex(x => new { x.IceAllocationId, x.Revision, x.PostingType }).IsUnique();
        entity.HasOne(x => x.IceAllocation).WithMany(x => x.InventoryPostings).HasForeignKey(x => x.IceAllocationId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.InventoryTransaction).WithMany().HasForeignKey(x => x.InventoryTransactionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.ApprovedByStaff).WithMany().HasForeignKey(x => x.ApprovedByStaffId).OnDelete(DeleteBehavior.Restrict);
    }
}
