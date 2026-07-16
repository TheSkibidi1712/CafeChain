using CafeChain.Models.Inventories.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Procurement;

public sealed class SupplierReceiptIssueConfiguration : IEntityTypeConfiguration<SupplierReceiptIssue>
{
    public void Configure(EntityTypeBuilder<SupplierReceiptIssue> b)
    {
        b.ToTable("SupplierReceiptIssues", table =>
            table.HasCheckConstraint("CK_SupplierReceiptIssue_AffectedQuantity", "[AffectedBaseQuantity] >= 0"));
        b.HasKey(x => x.SupplierReceiptIssueId);
        b.Property(x => x.IssueType).HasMaxLength(40).IsRequired();
        b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        b.Property(x => x.AffectedBaseQuantity).HasPrecision(18, 3);
        b.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        b.Property(x => x.ResolutionNote).HasMaxLength(1000);
        b.Property(x => x.DismissReason).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.StoreId, x.SupplierId, x.ReportedAtUtc });
        b.HasIndex(x => new { x.BranchReceiptId, x.Status });
        b.HasIndex(x => x.BranchReceiptLineId);
        b.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PurchaseOrder).WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PurchaseOrderLine).WithMany().HasForeignKey(x => x.PurchaseOrderLineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.BranchReceipt).WithMany().HasForeignKey(x => x.BranchReceiptId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.BranchReceiptLine).WithMany().HasForeignKey(x => x.BranchReceiptLineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ReportedByStaff).WithMany().HasForeignKey(x => x.ReportedByStaffId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ResolvedByStaff).WithMany().HasForeignKey(x => x.ResolvedByStaffId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.DismissedByStaff).WithMany().HasForeignKey(x => x.DismissedByStaffId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SupplierReceiptIssueTransitionConfiguration : IEntityTypeConfiguration<SupplierReceiptIssueTransition>
{
    public void Configure(EntityTypeBuilder<SupplierReceiptIssueTransition> b)
    {
        b.ToTable("SupplierReceiptIssueTransitions");
        b.HasKey(x => x.SupplierReceiptIssueTransitionId);
        b.Property(x => x.PreviousStatus).HasMaxLength(20).IsRequired();
        b.Property(x => x.NewStatus).HasMaxLength(20).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.HasIndex(x => new { x.SupplierReceiptIssueId, x.OccurredAtUtc });
        b.HasOne(x => x.SupplierReceiptIssue).WithMany(x => x.Transitions)
            .HasForeignKey(x => x.SupplierReceiptIssueId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.ActorStaff).WithMany().HasForeignKey(x => x.ActorStaffId).OnDelete(DeleteBehavior.Restrict);
    }
}
