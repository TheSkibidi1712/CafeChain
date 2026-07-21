using CafeChain.Models.Inventories.Transfers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Transfers;

public sealed class InventoryTransferDiscrepancyPostingConfiguration
    : IEntityTypeConfiguration<InventoryTransferDiscrepancyPosting>
{
    public void Configure(EntityTypeBuilder<InventoryTransferDiscrepancyPosting> entity)
    {
        entity.ToTable("InventoryTransferDiscrepancyPostings", table =>
        {
            table.HasCheckConstraint(
                "CK_InventoryTransferDiscrepancyPosting_QuantityCost",
                "[Quantity] > 0 AND [UnitCost] > 0 AND [TotalCost] >= 0");
        });

        entity.HasKey(x => x.InventoryTransferDiscrepancyPostingId);
        entity.Property(x => x.PostingType).HasConversion<int>().IsRequired();
        entity.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
        entity.Property(x => x.UnitCost).HasColumnType("decimal(18,6)");
        entity.Property(x => x.TotalCost).HasColumnType("decimal(18,2)");
        entity.Property(x => x.RequestKey).HasMaxLength(100).IsRequired();
        entity.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        entity.Property(x => x.CreatedAt).IsRequired();

        entity.HasOne(x => x.InventoryTransferDetail)
            .WithMany(x => x.DiscrepancyPostings)
            .HasForeignKey(x => x.InventoryTransferDetailId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.InventoryTransferCostAllocation)
            .WithMany(x => x.DiscrepancyPostings)
            .HasForeignKey(x => x.InventoryTransferCostAllocationId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.RelatedPosting)
            .WithMany(x => x.RelatedPostings)
            .HasForeignKey(x => x.RelatedPostingId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(x => x.ActorStaff)
            .WithMany()
            .HasForeignKey(x => x.ActorStaffId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(x => new
            {
                x.RequestKey,
                x.InventoryTransferDetailId,
                x.PostingType,
                x.InventoryTransferCostAllocationId,
                x.RelatedPostingId
            })
            .IsUnique()
            .HasFilter(null)
            .HasDatabaseName("UX_TransferDiscrepancyPosting_Request_Line_Type_Cost");
        entity.HasIndex(x => new { x.InventoryTransferDetailId, x.PostingType });
        entity.HasIndex(x => x.RelatedPostingId);
        entity.HasIndex(x => x.ActorStaffId);
    }
}
