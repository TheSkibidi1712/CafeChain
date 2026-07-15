using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Documents
{
    public class InventoryDocumentConfiguration : IEntityTypeConfiguration<InventoryDocument>
    {
        public void Configure(EntityTypeBuilder<InventoryDocument> entity)
        {
            entity.ToTable("InventoryDocuments");

            entity.HasKey(x => x.InventoryDocumentId);

            // ================= BASIC =================

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(x => x.Code)
                .IsUnique();

            entity.Property(x => x.Type)
                .IsRequired();

            entity.Property(x => x.Status)
                .IsRequired();

            entity.Property(x => x.Purpose)
                .IsRequired();

            entity.Property(x => x.DocumentDate)
                .HasDefaultValueSql("GETDATE()");

            entity.Property(x => x.Note)
                .HasMaxLength(500);

            entity.Property(x => x.NegativeReason)
                .HasMaxLength(1000);

            // ================= IDEMPOTENCY =================

            entity.Property(x => x.RequestKey)
                .HasMaxLength(100);

            entity.HasIndex(x => x.RequestKey)
                .IsUnique()
                .HasFilter("[RequestKey] IS NOT NULL");

            entity.Property(x => x.IsProcessing)
                .HasDefaultValue(false);

            entity.Property(x => x.RowVersion)
                .IsRowVersion();

            // ================= PARTNER =================

            entity.Property(x => x.PartnerType)
                .HasDefaultValue(InventoryPartnerType.NONE);

            entity.Property(x => x.PartnerName)
                .HasMaxLength(200);

            // ================= MONEY =================

            entity.Property(x => x.TotalAmount)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.VatAmount)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.FinalAmount)
                .HasColumnType("decimal(18,2)");

            // ================= RELATION =================

            entity.HasOne(x => x.Store)
                .WithMany(x => x.InventoryDocuments)
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Staff)
                .WithMany(x => x.InventoryDocuments)
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Supplier)
                .WithMany(x => x.InventoryDocuments)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.SetNull);

            // ================= INDEX =================

            entity.HasIndex(x => new
            {
                x.StoreId,
                x.DocumentDate
            });

            entity.HasIndex(x => x.Type);

            entity.HasIndex(x => x.Status);

            entity.HasIndex(x => x.SupplierId);

            entity.HasIndex(x => x.DocumentDate);

            entity.HasIndex(x => new
            {
                x.StoreId,
                x.Type,
                x.Status
            });

            entity.HasIndex(x => new
            {
                x.StoreId,
                x.Purpose
            });
        }
    }
}
