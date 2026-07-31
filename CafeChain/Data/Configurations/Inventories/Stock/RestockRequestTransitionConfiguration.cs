using CafeChain.Models.Inventories.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Stock
{
    public class RestockRequestTransitionConfiguration : IEntityTypeConfiguration<RestockRequestTransition>
    {
        public void Configure(EntityTypeBuilder<RestockRequestTransition> entity)
        {
            entity.ToTable("RestockRequestTransitions");

            entity.HasKey(x => x.RestockRequestTransitionId);

            entity.Property(x => x.PreviousStatus)
                .IsRequired()
                .HasMaxLength(32);

            entity.Property(x => x.NewStatus)
                .IsRequired()
                .HasMaxLength(32);

            entity.Property(x => x.Reason)
                .HasMaxLength(500);

            entity.Property(x => x.RequestKey)
                .HasMaxLength(100);

            entity.Property(x => x.SuggestionSnapshotVersion)
                .HasMaxLength(32);

            entity.Property(x => x.SuggestionSnapshotJson)
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.QuantityBefore)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.QuantityAfter)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.OccurredAtUtc).IsRequired();

            entity.HasOne(x => x.RestockRequest)
                .WithMany()
                .HasForeignKey(x => x.RestockRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ActorStaff)
                .WithMany()
                .HasForeignKey(x => x.ActorStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.BranchReceipt)
                .WithMany()
                .HasForeignKey(x => x.BranchReceiptId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.InventoryTransaction)
                .WithMany()
                .HasForeignKey(x => x.InventoryTransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.InventoryTransfer)
                .WithMany()
                .HasForeignKey(x => x.InventoryTransferId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.RestockRequestId);
            entity.HasIndex(x => x.OccurredAtUtc);
            entity.HasIndex(x => new { x.RestockRequestId, x.OccurredAtUtc });
            entity.HasIndex(x => x.InventoryTransferId);
        }
    }
}
