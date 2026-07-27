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

            entity.Property(x => x.SourceType)
                .IsRequired()
                .HasMaxLength(32)
                .HasDefaultValue("Legacy");
            entity.Property(x => x.SourceReferenceId).HasMaxLength(100);
            entity.Property(x => x.RequestedProcurementQuantity).HasPrecision(18, 3);
            entity.Property(x => x.TargetStockProcurementQuantity).HasPrecision(18, 3);
            entity.Property(x => x.ForecastEvidence).HasMaxLength(1000);
            entity.Property(x => x.SourcingDecision).HasMaxLength(24);
            entity.Property(x => x.SourcingStatus)
                .IsRequired()
                .HasMaxLength(24)
                .HasDefaultValue("UNALLOCATED");

            entity.Property(x => x.Priority)
                .IsRequired()
                .HasMaxLength(16);

            entity.Property(x => x.Note)
                .HasMaxLength(500);

            entity.Property(x => x.ProcessingNote).HasMaxLength(500);
            entity.Property(x => x.ClosedRemainingQuantity)
                .HasColumnType("decimal(18,3)")
                .HasDefaultValue(0m);
            entity.Property(x => x.RemainingCloseReason).HasMaxLength(500);

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

            entity.HasOne(x => x.CreatedForStore)
                .WithMany()
                .HasForeignKey(x => x.CreatedForStoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ProcurementUnit)
                .WithMany()
                .HasForeignKey(x => x.ProcurementUnitId)
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

            entity.HasOne(x => x.AcceptedByStaff)
                .WithMany()
                .HasForeignKey(x => x.AcceptedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.RemainingClosedByStaff)
                .WithMany()
                .HasForeignKey(x => x.RemainingClosedByStaffId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.StoreId);
            entity.HasIndex(x => new { x.StoreId, x.SourceType, x.Status });
            entity.HasIndex(x => new { x.StoreId, x.SourcingStatus });
            entity.HasIndex(x => x.StockAlertId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CreatedByStaffId);
            entity.HasIndex(x => x.AcceptedByStaffId);
            entity.HasIndex(x => x.RemainingClosedByStaffId);
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
