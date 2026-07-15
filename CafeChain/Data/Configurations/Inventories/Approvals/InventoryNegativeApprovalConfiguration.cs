using CafeChain.Models.Inventories.Approvals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Approvals;

public sealed class InventoryNegativeApprovalConfiguration : IEntityTypeConfiguration<InventoryNegativeApproval>
{
    public void Configure(EntityTypeBuilder<InventoryNegativeApproval> entity)
    {
        entity.ToTable("InventoryNegativeApprovals", table => table.HasCheckConstraint(
            "CK_InventoryNegativeApproval_Status",
            "[Status] IN ('REQUESTED','APPROVED','REJECTED','CANCELLED')"));
        entity.HasKey(x => x.InventoryNegativeApprovalId);
        entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
        entity.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        entity.Property(x => x.ReviewNote).HasMaxLength(1000);
        entity.Property(x => x.PolicyVersion).HasMaxLength(100).IsRequired();
        entity.Property(x => x.RequestKey).HasMaxLength(200).IsRequired();
        entity.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
        entity.Property(x => x.RowVersion).IsRowVersion();
        entity.HasIndex(x => x.InventoryDocumentId).IsUnique();
        entity.HasIndex(x => new { x.RequestKey, x.RequesterStaffId }).IsUnique();
        entity.HasIndex(x => new { x.Status, x.RequestedAt });
        entity.HasOne(x => x.InventoryDocument).WithOne(x => x.NegativeApproval).HasForeignKey<InventoryNegativeApproval>(x => x.InventoryDocumentId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.RequesterStaff).WithMany().HasForeignKey(x => x.RequesterStaffId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.ApproverStaff).WithMany().HasForeignKey(x => x.ApproverStaffId).OnDelete(DeleteBehavior.Restrict);
    }
}
