using CafeChain.Models.Inventories.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Stock
{
    public class BranchReceiptConfiguration : IEntityTypeConfiguration<BranchReceipt>
    {
        public void Configure(EntityTypeBuilder<BranchReceipt> entity)
        {
            entity.ToTable("BranchReceipts");

            entity.HasKey(x => x.BranchReceiptId);

            entity.Property(x => x.ReceiptCode)
                .IsRequired()
                .HasMaxLength(40);

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(16);

            entity.Property(x => x.ReceiptKey)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.ReferenceNumber)
                .HasMaxLength(100);

            entity.Property(x => x.Notes)
                .HasMaxLength(1000);

            entity.Property(x => x.CreatedAt).IsRequired();
            entity.Property(x => x.ReceivedAt).IsRequired();

            entity.Property(x => x.RowVersion)
                .IsRowVersion();

            entity.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Supplier)
                .WithMany()
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SourceInventoryTransfer)
                .WithMany()
                .HasForeignKey(x => x.SourceInventoryTransferId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReceivedByStaff)
                .WithMany()
                .HasForeignKey(x => x.ReceivedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ConfirmedByStaff)
                .WithMany()
                .HasForeignKey(x => x.ConfirmedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CreatedByStaff)
                .WithMany()
                .HasForeignKey(x => x.CreatedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.StoreId, x.ReceiptKey })
                .IsUnique()
                .HasDatabaseName("UX_BranchReceipts_Store_ReceiptKey");

            entity.HasIndex(x => x.StoreId);
            entity.HasIndex(x => x.SourceInventoryTransferId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.ReceiptCode)
                .IsUnique()
                .HasDatabaseName("UX_BranchReceipts_ReceiptCode");
        }
    }
}
