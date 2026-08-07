using CafeChain.Models.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Operations;

public sealed class WorkShiftOpenApprovalRequestConfiguration : IEntityTypeConfiguration<WorkShiftOpenApprovalRequest>
{
    public void Configure(EntityTypeBuilder<WorkShiftOpenApprovalRequest> entity)
    {
        entity.ToTable("WorkShiftOpenApprovalRequests");
        entity.HasKey(x => x.WorkShiftOpenApprovalRequestId);
        entity.Property(x => x.PublicId).IsRequired();
        entity.Property(x => x.RequestKey).IsRequired().HasMaxLength(200);
        entity.Property(x => x.TerminalId).IsRequired().HasMaxLength(100);
        entity.Property(x => x.Reason).IsRequired().HasMaxLength(500);
        entity.Property(x => x.Status).IsRequired().HasMaxLength(40);
        entity.Property(x => x.DecisionReason).HasMaxLength(500);
        entity.Property(x => x.RowVersion).IsRowVersion();
        entity.HasIndex(x => x.PublicId).IsUnique();
        entity.HasIndex(x => x.RequestKey).IsUnique();
        entity.HasIndex(x => new { x.StoreId, x.Status, x.RequestedAtUtc });
        entity.HasIndex(x => new { x.RequestedByStaffId, x.Status });
        entity.HasIndex(x => new { x.StoreId, x.RequestedByStaffId, x.TerminalId })
            .IsUnique()
            .HasFilter("[Status] = 'PENDING'")
            .HasDatabaseName("UX_WorkShiftOpenApprovals_ActiveContext");
        entity.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.RequestedByStaff).WithMany().HasForeignKey(x => x.RequestedByStaffId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.DecidedByStaff).WithMany().HasForeignKey(x => x.DecidedByStaffId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.SourceStaffShift).WithMany().HasForeignKey(x => x.SourceStaffShiftId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Terminal).WithMany().HasForeignKey(x => x.TerminalId).OnDelete(DeleteBehavior.Restrict);
    }
}
