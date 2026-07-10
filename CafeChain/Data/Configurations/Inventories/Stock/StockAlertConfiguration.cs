using CafeChain.Models.Inventories.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Stock
{
    public class StockAlertConfiguration : IEntityTypeConfiguration<StockAlert>
    {
        public void Configure(EntityTypeBuilder<StockAlert> entity)
        {
            entity.ToTable("StockAlerts", t =>
            {
                t.HasCheckConstraint(
                    "CK_StockAlerts_XOR_Item",
                    "([IngredientId] IS NOT NULL AND [RecipeId] IS NULL) OR ([IngredientId] IS NULL AND [RecipeId] IS NOT NULL)");
            });

            entity.HasKey(x => x.StockAlertId);

            entity.Property(x => x.AlertType)
                .IsRequired()
                .HasMaxLength(32);

            entity.Property(x => x.Severity)
                .IsRequired()
                .HasMaxLength(16);

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(32);

            entity.Property(x => x.Source)
                .IsRequired()
                .HasMaxLength(32);

            entity.Property(x => x.CurrentQtySnapshot)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.ThresholdSnapshot)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.Note)
                .HasMaxLength(500);

            entity.Property(x => x.ResolvedReason)
                .HasMaxLength(500);

            entity.Property(x => x.CreatedAt)
                .IsRequired();

            entity.Property(x => x.UpdatedAt)
                .IsRequired();

            entity.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Ingredient)
                .WithMany()
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Recipe)
                .WithMany()
                .HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.StoreId);
            entity.HasIndex(x => new { x.StoreId, x.IngredientId });
            entity.HasIndex(x => new { x.StoreId, x.RecipeId });
            entity.HasIndex(x => x.Status);

            // At most one OPEN alert per store ingredient / recipe (SQL Server filtered unique).
            // Service-layer duplicate guard remains source of truth for all providers.
            entity.HasIndex(x => new { x.StoreId, x.IngredientId })
                .IsUnique()
                .HasFilter("[IngredientId] IS NOT NULL AND [Status] = 'OPEN'")
                .HasDatabaseName("UX_StockAlert_Open_Store_Ingredient");

            entity.HasIndex(x => new { x.StoreId, x.RecipeId })
                .IsUnique()
                .HasFilter("[RecipeId] IS NOT NULL AND [Status] = 'OPEN'")
                .HasDatabaseName("UX_StockAlert_Open_Store_Recipe");
        }
    }
}
