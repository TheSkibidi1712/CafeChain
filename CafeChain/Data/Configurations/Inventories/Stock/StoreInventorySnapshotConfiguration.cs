using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Stock
{
    public class StoreInventorySnapshotConfiguration : IEntityTypeConfiguration<StoreInventorySnapshot>
    {
        public void Configure(EntityTypeBuilder<StoreInventorySnapshot> entity)
        {
            entity.ToTable("StoreInventorySnapshots", table =>
            {
                table.HasCheckConstraint(
                    "CK_StoreInventorySnapshot_Quantity",
                    "[Quantity] >= 0"
                );

                table.HasCheckConstraint(
                    "CK_StoreInventorySnapshot_AvgCost",
                    "[AvgCost] >= 0"
                );
            });

            entity.HasKey(x => x.StoreInventorySnapshotId);

            // ================= PROPERTY =================

            entity.Property(x => x.Quantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.AvgCost)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(x => x.UpdatedAt)
                .HasDefaultValueSql("GETDATE()");

            // ================= RELATION =================

            entity.HasOne<Store>()
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Ingredient>()
                .WithMany()
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            entity.HasIndex(x => new
            {
                x.StoreId,
                x.IngredientId
            }).IsUnique();

            entity.HasIndex(x => x.StoreId);

            entity.HasIndex(x => x.IngredientId);

            entity.HasIndex(x => x.UpdatedAt);
        }
    }
}

