using CafeChain.Models.Inventories.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Procurement;

public sealed class PurchaseAdviceFulfillmentPostingConfiguration : IEntityTypeConfiguration<PurchaseAdviceFulfillmentPosting>
{
    public void Configure(EntityTypeBuilder<PurchaseAdviceFulfillmentPosting> b)
    {
        b.ToTable("PurchaseAdviceFulfillmentPostings", table =>
        {
            table.HasCheckConstraint("CK_PurchaseAdviceFulfillmentPostings_QuantityPositive", "[Quantity] > 0");
            table.HasCheckConstraint(
                "CK_PurchaseAdviceFulfillmentPostings_Type",
                "[PostingType] IN ('ACCEPTED','CLOSED')");
            table.HasCheckConstraint(
                "CK_PurchaseAdviceFulfillmentPostings_SourceByType",
                "([PostingType] = 'ACCEPTED' AND [BranchReceiptLineId] IS NOT NULL AND [CloseOperationKey] IS NULL) OR " +
                "([PostingType] = 'CLOSED' AND [BranchReceiptLineId] IS NULL AND [CloseOperationKey] IS NOT NULL)");
        });

        b.HasKey(x => x.PurchaseAdviceFulfillmentPostingId);
        b.Property(x => x.PostingType).HasMaxLength(20).IsRequired();
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.SourceDocumentType).HasMaxLength(40).IsRequired();
        b.Property(x => x.CloseOperationKey).HasMaxLength(100);
        b.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.CreatedAtUtc).IsRequired();

        b.HasIndex(x => new { x.PurchaseAdviceLineId, x.CreatedAtUtc });
        b.HasIndex(x => new { x.PurchaseOrderLineAllocationId, x.PostingType });
        b.HasIndex(x => new { x.BranchReceiptLineId, x.PurchaseOrderLineAllocationId, x.PostingType })
            .IsUnique()
            .HasFilter("[BranchReceiptLineId] IS NOT NULL AND [PostingType] = 'ACCEPTED'");
        b.HasIndex(x => new { x.CloseOperationKey, x.PurchaseOrderLineAllocationId, x.PostingType })
            .IsUnique()
            .HasFilter("[CloseOperationKey] IS NOT NULL AND [PostingType] = 'CLOSED'");
        b.HasIndex(x => new
        {
            x.SourceDocumentType,
            x.SourceDocumentId,
            x.SourceDocumentLineId,
            x.PostingType,
            x.PurchaseAdviceLineId
        }).IsUnique();

        b.HasOne(x => x.PurchaseAdviceLine)
            .WithMany()
            .HasForeignKey(x => x.PurchaseAdviceLineId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PurchaseOrderLineAllocation)
            .WithMany()
            .HasForeignKey(x => x.PurchaseOrderLineAllocationId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PurchaseOrderLine)
            .WithMany()
            .HasForeignKey(x => x.PurchaseOrderLineId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.BranchReceiptLine)
            .WithMany()
            .HasForeignKey(x => x.BranchReceiptLineId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.BaseUnit)
            .WithMany()
            .HasForeignKey(x => x.BaseUnitId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ActorStaff)
            .WithMany()
            .HasForeignKey(x => x.ActorStaffId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
