using CafeChain.Models.Inventories.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Documents
{
    public class InventoryDocumentSnapshotDetailConfiguration : IEntityTypeConfiguration<InventoryDocumentSnapshotDetail>
    {
        public void Configure(EntityTypeBuilder<InventoryDocumentSnapshotDetail> entity)
        {
            entity.ToTable("InventoryDocumentSnapshotDetails");

            entity.HasKey(x => x.Id);

            // ================= BASIC =================

            entity.Property(x => x.ItemName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.UnitName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.Quantity)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.UnitPrice)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.TotalAmount)
                .HasColumnType("decimal(18,2)");

            // ================= RELATION =================

            entity.HasOne(x => x.InventoryDocumentSnapshot)
                .WithMany(x => x.Details)
                .HasForeignKey(x => x.InventoryDocumentSnapshotId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================= INDEX =================

            entity.HasIndex(x => x.InventoryDocumentSnapshotId);
        }
    }
}
