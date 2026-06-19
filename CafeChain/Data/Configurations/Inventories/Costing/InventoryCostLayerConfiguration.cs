using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Costing
{
    public class InventoryCostLayerConfiguration : IEntityTypeConfiguration<InventoryCostLayer>
    {
        public void Configure(EntityTypeBuilder<InventoryCostLayer> entity)
        {
            entity.ToTable("InventoryCostLayers");

            entity.HasKey(x => x.InventoryCostLayerId);

            // ================= BASIC =================

            entity.Property(x => x.Quantity)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.RemainingQuantity)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.UnitCost)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // ================= RELATION =================

            entity.HasOne<Ingredient>()
                .WithMany()
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Store>()
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            entity.HasIndex(x => x.IngredientId);

            entity.HasIndex(x => x.StoreId);

            entity.HasIndex(x => x.CreatedAt);

            entity.HasIndex(x => new
            {
                x.StoreId,
                x.IngredientId
            });

            entity.HasIndex(x => new
            {
                x.StoreId,
                x.IngredientId,
                x.RemainingQuantity
            });
        }
    }

}
