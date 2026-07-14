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
                // RecipeId may remain as compatibility metadata, but is never inventory identity.
                t.HasCheckConstraint(
                    "CK_StockAlerts_Identity",
                    @"
(
  ([IngredientId] IS NOT NULL AND [RecipeId] IS NULL AND [PreparedItemId] IS NULL)
  OR ([IngredientId] IS NULL AND [RecipeId] IS NOT NULL AND [PreparedItemId] IS NOT NULL)
  OR ([IngredientId] IS NULL AND [RecipeId] IS NULL AND [PreparedItemId] IS NOT NULL)
)");
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

            entity.Property(x => x.ManagerNote)
                .HasMaxLength(500);

            entity.Property(x => x.RejectReason)
                .HasMaxLength(500);

            entity.Property(x => x.ResolvedReason)
                .HasMaxLength(500);

            entity.Property(x => x.CreatedAt)
                .IsRequired();

            entity.Property(x => x.UpdatedAt)
                .IsRequired();

            entity.Property(x => x.RowVersion)
                .IsRowVersion();

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

            entity.HasOne(x => x.PreparedItem)
                .WithMany()
                .HasForeignKey(x => x.PreparedItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ReportedByStaff)
                .WithMany()
                .HasForeignKey(x => x.ReportedByStaffId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.ConfirmedByStaff)
                .WithMany()
                .HasForeignKey(x => x.ConfirmedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RejectedByStaff)
                .WithMany()
                .HasForeignKey(x => x.RejectedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.StoreId);
            entity.HasIndex(x => new { x.StoreId, x.IngredientId });
            entity.HasIndex(x => new { x.StoreId, x.RecipeId });
            entity.HasIndex(x => new { x.StoreId, x.PreparedItemId });
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.PreparedItemId)
                .HasDatabaseName("IX_StockAlerts_PreparedItemId");

            // At most one active alert per canonical store inventory identity.
            entity.HasIndex(x => new { x.StoreId, x.IngredientId })
                .IsUnique()
                .HasFilter("[IngredientId] IS NOT NULL AND [Status] IN ('OPEN','CONFIRMED')")
                .HasDatabaseName("UX_StockAlert_Active_Store_Ingredient");

            entity.HasIndex(x => new { x.StoreId, x.PreparedItemId })
                .IsUnique()
                .HasFilter("[PreparedItemId] IS NOT NULL AND [Status] IN ('OPEN','CONFIRMED')")
                .HasDatabaseName("UX_StockAlert_Active_Store_PreparedItem");
        }
    }
}
