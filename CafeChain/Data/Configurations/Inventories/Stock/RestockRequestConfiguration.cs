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
                t.HasCheckConstraint(
                    "CK_RestockRequests_XOR_Item",
                    "([IngredientId] IS NOT NULL AND [RecipeId] IS NULL) OR ([IngredientId] IS NULL AND [RecipeId] IS NOT NULL)");
            });

            entity.HasKey(x => x.RestockRequestId);

            entity.Property(x => x.RequestedQuantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.SuggestedQuantity)
                .HasColumnType("decimal(18,3)");

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

            // Restrict FKs — avoid SQL Server multiple cascade paths (#99 lesson).
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

            // At most one SUBMITTED request per StockAlert (service guard remains source of truth).
            entity.HasIndex(x => x.StockAlertId)
                .IsUnique()
                .HasFilter("[Status] = 'SUBMITTED'")
                .HasDatabaseName("UX_RestockRequest_Open_StockAlert");
        }
    }
}
