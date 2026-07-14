using CafeChain.Models.Inventories.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Stock
{
    public class RestockFulfillmentPostingConfiguration : IEntityTypeConfiguration<RestockFulfillmentPosting>
    {
        public void Configure(EntityTypeBuilder<RestockFulfillmentPosting> entity)
        {
            entity.ToTable("RestockFulfillmentPostings", table =>
            {
                table.HasCheckConstraint(
                    "CK_RestockFulfillmentPosting_Identity",
                    "([IngredientId] IS NOT NULL AND [PreparedItemId] IS NULL) OR ([IngredientId] IS NULL AND [PreparedItemId] IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_RestockFulfillmentPosting_Quantity",
                    "[Quantity] > 0");
            });

            entity.HasKey(x => x.RestockFulfillmentPostingId);
            entity.Property(x => x.SourceDocumentType).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Quantity).HasColumnType("decimal(18,3)").IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();

            entity.HasOne(x => x.RestockRequest)
                .WithMany(x => x.FulfillmentPostings)
                .HasForeignKey(x => x.RestockRequestId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Ingredient)
                .WithMany()
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PreparedItem)
                .WithMany()
                .HasForeignKey(x => x.PreparedItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.BaseUnit)
                .WithMany()
                .HasForeignKey(x => x.BaseUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new
                {
                    x.SourceDocumentType,
                    x.SourceDocumentId,
                    x.SourceDocumentLineId,
                    x.RestockRequestId
                })
                .IsUnique()
                .HasDatabaseName("UX_RestockFulfillmentPosting_SourceLine_Request");
            entity.HasIndex(x => new { x.RestockRequestId, x.CreatedAtUtc });
        }
    }
}
