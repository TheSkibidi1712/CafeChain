using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Production;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Costing
{
    public class InventoryCostLayerConfiguration : IEntityTypeConfiguration<InventoryCostLayer>
    {
        public void Configure(EntityTypeBuilder<InventoryCostLayer> entity)
        {
            entity.ToTable("InventoryCostLayers", table =>
            {
                table.HasCheckConstraint(
                    "CK_InventoryCostLayers_ExactlyOneIdentity",
                    "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
            });

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

            entity.HasOne<PreparedItem>()
                .WithMany()
                .HasForeignKey(x => x.PreparedItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Store>()
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SourceProductionRun)
                .WithMany()
                .HasForeignKey(x => x.SourceProductionRunId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            entity.HasIndex(x => x.IngredientId);

            entity.HasIndex(x => x.PreparedItemId);

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

            entity.HasIndex(x => new
            {
                x.StoreId,
                x.PreparedItemId
            });

            entity.HasIndex(x => new
            {
                x.StoreId,
                x.PreparedItemId,
                x.RemainingQuantity
            });

            // One output layer per ProductionRun
            entity.HasIndex(x => x.SourceProductionRunId)
                .IsUnique()
                .HasFilter("[SourceProductionRunId] IS NOT NULL")
                .HasDatabaseName("UX_InventoryCostLayers_SourceProductionRunId");
        }
    }
}
