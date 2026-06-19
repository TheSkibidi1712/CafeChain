using CafeChain.Models.Inventories.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Documents
{
    public class InventoryDocumentSnapshotConfiguration : IEntityTypeConfiguration<InventoryDocumentSnapshot>
    {
        public void Configure(EntityTypeBuilder<InventoryDocumentSnapshot> entity)
        {
            entity.ToTable("InventoryDocumentSnapshots");

            entity.HasKey(x => x.InventoryDocumentSnapshotId);

            // ================= BASIC =================

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.StoreName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.StaffName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.PartnerName)
                .HasMaxLength(200);

            entity.Property(x => x.TotalAmount)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.VatAmount)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.FinalAmount)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // ================= RELATION =================

            entity.HasMany(x => x.Details)
                .WithOne(x => x.InventoryDocumentSnapshot)
                .HasForeignKey(x => x.InventoryDocumentSnapshotId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================= INDEX =================

            entity.HasIndex(x => x.InventoryDocumentId);

            entity.HasIndex(x => x.Code);

            entity.HasIndex(x => x.DocumentDate);

            entity.HasIndex(x => x.CreatedAt);
        }
    }
}
