using CafeChain.Models.Inventories.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Documents
{
    public class InventoryDocumentDetailConfiguration : IEntityTypeConfiguration<InventoryDocumentDetail>
    {
        public void Configure(EntityTypeBuilder<InventoryDocumentDetail> entity)
        {
            entity.ToTable("InventoryDocumentDetails");

            entity.HasKey(x => x.InventoryDocumentDetailId);

            // ================= BASIC =================

            entity.Property(x => x.Quantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.BaseQuantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.UnitPrice)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.CostPrice)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.CostAmount)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.TotalAmount)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.Note)
                .HasMaxLength(500);

            // ================= RELATION =================

            entity.HasOne(x => x.InventoryDocument)
                .WithMany(x => x.Details)
                .HasForeignKey(x => x.InventoryDocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Ingredient)
                .WithMany(x => x.InventoryDocumentDetails)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Unit)
                .WithMany(x => x.InventoryDocumentDetails)
                .HasForeignKey(x => x.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            entity.HasIndex(x => x.InventoryDocumentId);

            entity.HasIndex(x => x.IngredientId);

            entity.HasIndex(x => x.UnitId);

            entity.HasIndex(x => new
            {
                x.InventoryDocumentId,
                x.IngredientId
            });
        }
    }
}
