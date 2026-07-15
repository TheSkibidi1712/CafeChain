using CafeChain.Models.Inventories.Transfers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Transfers
{
    public class InventoryTransferConfiguration : IEntityTypeConfiguration<InventoryTransfer>
    {
        public void Configure(EntityTypeBuilder<InventoryTransfer> entity)
        {
            entity.ToTable("InventoryTransfers", table =>
            {
                table.HasCheckConstraint(
                    "CK_InventoryTransfer_DifferentStore",
                    "[FromStoreId] <> [ToStoreId]");
            });

            entity.HasKey(x => x.InventoryTransferId);

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.RequestKey)
                .HasMaxLength(100);

            entity.Property(x => x.Type)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(x => x.Purpose)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(x => x.Status)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(x => x.DocumentDate)
                .HasDefaultValueSql("GETDATE()")
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()")
                .IsRequired();

            entity.Property(x => x.Note)
                .HasMaxLength(500);

            entity.Property(x => x.RowVersion).IsRowVersion();

            entity.HasOne(x => x.FromStore)
                .WithMany(x => x.FromTransfers)
                .HasForeignKey(x => x.FromStoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ToStore)
                .WithMany(x => x.ToTransfers)
                .HasForeignKey(x => x.ToStoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CreatedByStaff)
                .WithMany()
                .HasForeignKey(x => x.CreatedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ConfirmedByStaff)
                .WithMany()
                .HasForeignKey(x => x.ConfirmedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CancelledByStaff)
                .WithMany()
                .HasForeignKey(x => x.CancelledByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Details)
                .WithOne(x => x.InventoryTransfer)
                .HasForeignKey(x => x.InventoryTransferId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.Code)
                .IsUnique();

            entity.HasIndex(x => x.RequestKey)
                .IsUnique()
                .HasFilter("[RequestKey] IS NOT NULL");

            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.DocumentDate);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => new { x.FromStoreId, x.ToStoreId });
            entity.HasIndex(x => new { x.FromStoreId, x.Status });
            entity.HasIndex(x => new { x.ToStoreId, x.Status });
            entity.HasIndex(x => new { x.CreatedByStaffId, x.CreatedAt });
        }
    }
}
