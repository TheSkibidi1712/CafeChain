using CafeChain.Models.Inventories.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Stock
{
    public class RestockRequestConfiguration : IEntityTypeConfiguration<RestockRequest>
    {
        public void Configure(EntityTypeBuilder<RestockRequest> entity)
        {
            entity.ToTable("RestockRequests", t =>
            {
                // RecipeId is compatibility metadata only; canonical identity is Ingredient XOR PreparedItem.
                t.HasCheckConstraint(
                    "CK_RestockRequests_Identity",
                    @"
(
  ([IngredientId] IS NOT NULL AND [RecipeId] IS NULL AND [PreparedItemId] IS NULL)
  OR ([IngredientId] IS NULL AND [RecipeId] IS NOT NULL AND [PreparedItemId] IS NOT NULL)
  OR ([IngredientId] IS NULL AND [RecipeId] IS NULL AND [PreparedItemId] IS NOT NULL)
)");
            });

            entity.HasKey(x => x.RestockRequestId);

            entity.Property(x => x.RequestedQuantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.SuggestedQuantity)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.SuggestionAvailableSnapshot).HasColumnType("decimal(18,3)");
            entity.Property(x => x.SuggestionMinLevelSnapshot).HasColumnType("decimal(18,3)");
            entity.Property(x => x.SuggestionAverageDailyUsageSnapshot).HasColumnType("decimal(18,5)");
            entity.Property(x => x.SuggestionIncomingQuantitySnapshot).HasColumnType("decimal(18,3)");
            entity.Property(x => x.SuggestionReason).HasMaxLength(500);

            entity.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(32);

            entity.Property(x => x.Priority)
                .IsRequired()
                .HasMaxLength(16);

            entity.Property(x => x.Note)
                .HasMaxLength(500);

            entity.Property(x => x.CreatedAt)
                .IsRequired();

            entity.Property(x => x.UpdatedAt)
                .IsRequired();

            entity.Property(x => x.RowVersion)
                .IsRowVersion();

            entity.HasOne(x => x.StockAlert)
                .WithMany()
                .HasForeignKey(x => x.StockAlertId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

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

            entity.HasOne(x => x.CreatedByStaff)
                .WithMany()
                .HasForeignKey(x => x.CreatedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.HandledByStaff)
                .WithMany()
                .HasForeignKey(x => x.HandledByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.StoreId);
            entity.HasIndex(x => x.StockAlertId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CreatedByStaffId);
            entity.HasIndex(x => x.PreparedItemId)
                .HasDatabaseName("IX_RestockRequests_PreparedItemId");

            entity.HasIndex(x => x.StockAlertId)
                .IsUnique()
                .HasFilter("[StockAlertId] IS NOT NULL AND [Status] IN ('DRAFT','SUBMITTED','PROCESSING','PARTIALLY_RECEIVED')")
                .HasDatabaseName("UX_RestockRequest_Active_StockAlert");

            entity.HasIndex(x => new { x.StoreId, x.IngredientId })
                .IsUnique()
                .HasFilter("[IngredientId] IS NOT NULL AND [Status] IN ('DRAFT','SUBMITTED','PROCESSING','PARTIALLY_RECEIVED')")
                .HasDatabaseName("UX_RestockRequest_Active_Store_Ingredient");

            entity.HasIndex(x => new { x.StoreId, x.PreparedItemId })
                .IsUnique()
                .HasFilter("[PreparedItemId] IS NOT NULL AND [Status] IN ('DRAFT','SUBMITTED','PROCESSING','PARTIALLY_RECEIVED')")
                .HasDatabaseName("UX_RestockRequest_Active_Store_PreparedItem");
        }
    }
}
