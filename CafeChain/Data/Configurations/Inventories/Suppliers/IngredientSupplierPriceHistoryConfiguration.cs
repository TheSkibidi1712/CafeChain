using CafeChain.Models.Inventories.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Suppliers
{
    public class IngredientSupplierPriceHistoryConfiguration : IEntityTypeConfiguration<IngredientSupplierPriceHistory>
    {
        public void Configure(EntityTypeBuilder<IngredientSupplierPriceHistory> entity)
        {
            entity.ToTable("IngredientSupplierPriceHistories", table =>
            {
                table.HasCheckConstraint(
                    "CK_IngredientSupplierPriceHistory_Price",
                    "[Price] >= 0"
                );

                table.HasCheckConstraint(
                    "CK_IngredientSupplierPriceHistory_PackageQuantity",
                    "[PackageQuantity] IS NULL OR [PackageQuantity] > 0"
                );
            });

            entity.HasKey(x => x.IngredientSupplierPriceHistoryId);

            // ================= PROPERTY =================

            entity.Property(x => x.Price)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(x => x.PackageQuantity)
                .HasColumnType("decimal(18,5)");

            entity.Property(x => x.EffectiveDate)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(x => x.CreatedAtUtc)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(x => x.IsCurrent)
                .HasDefaultValue(true);

            entity.Property(x => x.Note)
                .HasMaxLength(1000);

            // ================= INDEX =================

            entity.HasIndex(x => x.IngredientSupplierId);

            entity.HasIndex(x => new
            {
                x.IngredientSupplierId,
                x.EffectiveDate
            });

            entity.HasIndex(x => x.PackageUnitId);

            entity.HasIndex(x => new { x.IngredientSupplierId, x.IsCurrent })
                .IsUnique()
                .HasFilter("[IsCurrent] = 1");

            entity.HasIndex(x => x.CreatedByStaffId);

            // ================= RELATION =================

            entity.HasOne(x => x.IngredientSupplier)
                .WithMany(x => x.PriceHistories)
                .HasForeignKey(x => x.IngredientSupplierId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.PackageUnit)
                .WithMany()
                .HasForeignKey(x => x.PackageUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= SEED =================
            // Snapshot package qty/unit only for approved parent offers (sugar #1, coffee #3).
            // Condensed milk #2 remains null package snapshot (ambiguous).

            entity.HasData(
                new IngredientSupplierPriceHistory
                {
                    IngredientSupplierPriceHistoryId = 1,
                    IngredientSupplierId = 1,
                    Price = 22000,
                    PackageQuantity = 1m,
                    PackageUnitId = 2, // kg
                    EffectiveDate = new DateTime(2025, 1, 1),
                    IsCurrent = true,
                    Note = "Giá ban đầu"
                },
                new IngredientSupplierPriceHistory
                {
                    IngredientSupplierPriceHistoryId = 2,
                    IngredientSupplierId = 2,
                    Price = 27000,
                    PackageQuantity = null,
                    PackageUnitId = null,
                    EffectiveDate = new DateTime(2025, 1, 1),
                    IsCurrent = true,
                    Note = "Giá ban đầu"
                },
                new IngredientSupplierPriceHistory
                {
                    IngredientSupplierPriceHistoryId = 3,
                    IngredientSupplierId = 3,
                    Price = 140000,
                    PackageQuantity = 1m,
                    PackageUnitId = 2, // kg
                    EffectiveDate = new DateTime(2025, 1, 1),
                    IsCurrent = true,
                    Note = "Giá ban đầu"
                }
            );
        }
    }
}
