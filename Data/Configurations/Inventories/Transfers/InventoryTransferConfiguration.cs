using CafeChain.Models.Inventories.Transfers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Transfers
{
    public class InventoryTransferConfiguration : IEntityTypeConfiguration<InventoryTransfer>
    {
        public void Configure(EntityTypeBuilder<InventoryTransfer> entity)
        {
            entity.ToTable("InventoryTransfers", t =>
            {
                t.HasCheckConstraint(
                    "CK_InventoryTransfer_DifferentStore",
                    "[FromStoreId] <> [ToStoreId]"
                );

                t.HasCheckConstraint(
                    "CK_InventoryTransfer_TotalExportQty",
                    "[TotalExportQty] >= 0"
                );

                t.HasCheckConstraint(
                    "CK_InventoryTransfer_TotalReceivedQty",
                    "[TotalReceivedQty] >= 0"
                );

                t.HasCheckConstraint(
                    "CK_InventoryTransfer_Received_NotGreater_Export",
                    "[TotalReceivedQty] <= [TotalExportQty]"
                );
            });

            entity.HasKey(x => x.InventoryTransferId);

            // ================= PROPERTY =================

            entity.Property(x => x.Status)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(x => x.TotalExportQty)
                .HasColumnType("decimal(18,3)")
                .HasDefaultValue(0);

            entity.Property(x => x.TotalReceivedQty)
                .HasColumnType("decimal(18,3)")
                .HasDefaultValue(0);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()")
                .IsRequired();

            // ================= RELATION : DOCUMENT =================

            entity.HasOne(x => x.ExportDocument)
                .WithOne(d => d.ExportTransfer)
                .HasForeignKey<InventoryTransfer>(x => x.ExportDocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ImportDocument)
                .WithOne(d => d.ImportTransfer)
                .HasForeignKey<InventoryTransfer>(x => x.ImportDocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= RELATION : STORE =================

            entity.HasOne(x => x.FromStore)
                .WithMany(s => s.ExportTransfers)
                .HasForeignKey(x => x.FromStoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ToStore)
                .WithMany(s => s.ImportTransfers)
                .HasForeignKey(x => x.ToStoreId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= RELATION : DETAIL =================

            entity.HasMany(x => x.Details)
                .WithOne(x => x.InventoryTransfer)
                .HasForeignKey(x => x.InventoryTransferId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================= INDEX =================

            entity.HasIndex(x => x.ExportDocumentId)
                .IsUnique();

            entity.HasIndex(x => x.ImportDocumentId)
                .IsUnique()
                .HasFilter("[ImportDocumentId] IS NOT NULL");

            entity.HasIndex(x => x.Status);

            entity.HasIndex(x => x.CreatedAt);

            entity.HasIndex(x => new
            {
                x.FromStoreId,
                x.ToStoreId
            });

            entity.HasIndex(x => new
            {
                x.FromStoreId,
                x.Status
            });

            entity.HasIndex(x => new
            {
                x.ToStoreId,
                x.Status
            });
        }
    }
}
