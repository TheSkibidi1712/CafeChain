using CafeChain.Models.Inventories.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Suppliers
{
    public class IngredientSupplierConfiguration : IEntityTypeConfiguration<IngredientSupplier>
    {
        public void Configure(EntityTypeBuilder<IngredientSupplier> entity)
        {
            entity.ToTable("IngredientSuppliers", table =>
            {
                table.HasCheckConstraint(
                    "CK_IngredientSupplier_CurrentPrice",
                    "[CurrentPrice] >= 0"
                );

                table.HasCheckConstraint(
                    "CK_IngredientSupplier_MOQ",
                    "[MinimumOrderQuantity] IS NULL OR [MinimumOrderQuantity] > 0"
                );

                table.HasCheckConstraint(
                    "CK_IngredientSupplier_LeadTime",
                    "[LeadTimeDays] IS NULL OR [LeadTimeDays] >= 0"
                );
            });

            entity.HasKey(x => x.IngredientSupplierId);

            // ================= PROPERTY =================

            entity.Property(x => x.CurrentPrice)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(x => x.MinimumOrderQuantity)
                .HasColumnType("decimal(18,3)");

            entity.Property(x => x.IsPrimary)
                .HasDefaultValue(false);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.Property(x => x.Note)
                .HasMaxLength(1000);

            // ================= INDEX =================

            entity.HasIndex(x => new
            {
                x.IngredientId,
                x.SupplierId
            }).IsUnique();

            entity.HasIndex(x => x.SupplierId);

            entity.HasIndex(x => x.IngredientId);

            entity.HasIndex(x => x.Active);

            // ================= RELATION =================

            entity.HasOne(x => x.Ingredient)
                .WithMany(x => x.IngredientSuppliers)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Supplier)
                .WithMany(x => x.IngredientSuppliers)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Unit)
                .WithMany()
                .HasForeignKey(x => x.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.PriceHistories)
                .WithOne(x => x.IngredientSupplier)
                .HasForeignKey(x => x.IngredientSupplierId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================= SEED =================

            entity.HasData(

                new IngredientSupplier
                {
                    IngredientSupplierId = 1,
                    IngredientId = 6,
                    SupplierId = 1,
                    CurrentPrice = 22000,
                    UnitId = 2,
                    MinimumOrderQuantity = 1,
                    LeadTimeDays = 1,
                    IsPrimary = true,
                    Active = true,
                    Note = "Đường Biên Hòa"
                },

                new IngredientSupplier
                {
                    IngredientSupplierId = 2,
                    IngredientId = 2,
                    SupplierId = 2,
                    CurrentPrice = 27000,
                    UnitId = 3,
                    MinimumOrderQuantity = 24,
                    LeadTimeDays = 2,
                    IsPrimary = true,
                    Active = true,
                    Note = "Sữa đặc Ông Thọ"
                },

                new IngredientSupplier
                {
                    IngredientSupplierId = 3,
                    IngredientId = 1,
                    SupplierId = 3,
                    CurrentPrice = 140000,
                    UnitId = 2,
                    MinimumOrderQuantity = 5,
                    LeadTimeDays = 3,
                    IsPrimary = true,
                    Active = true,
                    Note = "Cà phê hạt"
                },

                new IngredientSupplier
                {
                    IngredientSupplierId = 4,
                    IngredientId = 8,
                    SupplierId = 4,
                    CurrentPrice = 250000,
                    UnitId = 3,
                    MinimumOrderQuantity = 6,
                    LeadTimeDays = 4,
                    IsPrimary = true,
                    Active = true,
                    Note = "Syrup Torani"
                },

                new IngredientSupplier
                {
                    IngredientSupplierId = 5,
                    IngredientId = 10,
                    SupplierId = 2,
                    CurrentPrice = 95000,
                    UnitId = 4,
                    MinimumOrderQuantity = 12,
                    LeadTimeDays = 2,
                    IsPrimary = true,
                    Active = true,
                    Note = "Kem béo Rich"
                },

                new IngredientSupplier
                {
                    IngredientSupplierId = 6,
                    IngredientId = 9,
                    SupplierId = 5,
                    CurrentPrice = 450000,
                    UnitId = 1,
                    MinimumOrderQuantity = 1,
                    LeadTimeDays = 5,
                    IsPrimary = true,
                    Active = true,
                    Note = "Matcha Nhật"
                },

                new IngredientSupplier
                {
                    IngredientSupplierId = 7,
                    IngredientId = 5,
                    SupplierId = 3,
                    CurrentPrice = 180000,
                    UnitId = 2,
                    MinimumOrderQuantity = 2,
                    LeadTimeDays = 3,
                    IsPrimary = false,
                    Active = true,
                    Note = "Bột cacao"
                },

                new IngredientSupplier
                {
                    IngredientSupplierId = 8,
                    IngredientId = 4,
                    SupplierId = 1,
                    CurrentPrice = 85000,
                    UnitId = 2,
                    MinimumOrderQuantity = 2,
                    LeadTimeDays = 2,
                    IsPrimary = false,
                    Active = true,
                    Note = "Bột sữa"
                },

                new IngredientSupplier
                {
                    IngredientSupplierId = 9,
                    IngredientId = 3,
                    SupplierId = 4,
                    CurrentPrice = 120000,
                    UnitId = 1,
                    MinimumOrderQuantity = 1,
                    LeadTimeDays = 5,
                    IsPrimary = true,
                    Active = true,
                    Note = "Trà Lipton"
                }
            );
        }
    }
}
