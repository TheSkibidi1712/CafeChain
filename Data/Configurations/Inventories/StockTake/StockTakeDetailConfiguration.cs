using CafeChain.Models.Inventories.StockTake;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.StockTake
{
    public class StockTakeDetailConfiguration : IEntityTypeConfiguration<StockTakeDetail>
    {
        public void Configure(EntityTypeBuilder<StockTakeDetail> entity)
        {
            entity.ToTable("StockTakeDetails", table =>
            {
                table.HasCheckConstraint(
                    "CK_StockTakeDetail_SystemQuantity",
                    "[SystemQuantity] >= 0"
                );

                table.HasCheckConstraint(
                    "CK_StockTakeDetail_ActualQuantity",
                    "[ActualQuantity] >= 0"
                );
            });

            entity.HasKey(x => x.StockTakeDetailId);

            // ================= PROPERTY =================

            entity.Property(x => x.SystemQuantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.ActualQuantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.Note)
                .HasMaxLength(500);

            // ================= RELATION =================

            entity.HasOne(x => x.StockTakeSession)
                .WithMany(x => x.Details)
                .HasForeignKey(x => x.StockTakeSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Ingredient)
                .WithMany()
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            entity.HasIndex(x => x.StockTakeSessionId);

            entity.HasIndex(x => x.IngredientId);

            entity.HasIndex(x => new
            {
                x.StockTakeSessionId,
                x.IngredientId
            }).IsUnique();
        }
    }
}
