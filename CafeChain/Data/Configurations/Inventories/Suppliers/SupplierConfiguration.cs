using CafeChain.Models.Enums.Inventory.Suppliers;
using CafeChain.Models.Inventories.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Suppliers
{
    public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
    {
        public void Configure(EntityTypeBuilder<Supplier> entity)
        {
            entity.ToTable("Suppliers");

            entity.HasKey(x => x.SupplierId);

            // ================= PROPERTY =================

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.TaxCode)
                .HasMaxLength(14);

            entity.Property(x => x.Address)
                .HasMaxLength(500);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(x => x.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(x => x.Note)
                .HasMaxLength(1000);

            entity.Property(x => x.RowVersion)
                .IsRowVersion();

            // ================= INDEX =================

            entity.HasIndex(x => x.Code)
                .HasDatabaseName("UX_Suppliers_Code")
                .IsUnique();

            entity.HasIndex(x => x.TaxCode)
                .HasDatabaseName("UX_Suppliers_TaxCode")
                .IsUnique()
                .HasFilter("[TaxCode] IS NOT NULL");

            entity.HasIndex(x => x.Name);

            entity.HasIndex(x => x.Active);

            // ================= RELATION =================

            entity.HasMany(x => x.Phones)
                .WithOne(x => x.Supplier)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Contacts)
                .WithOne(x => x.Supplier)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.IngredientSuppliers)
                .WithOne(x => x.Supplier)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= SEED =================

            entity.HasData(
                new Supplier
                {
                    SupplierId = 1,
                    Code = "SUP001",
                    Name = "Nhà cung cấp A",
                    Address = "Bình Dương",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1),
                    UpdatedAt = new DateTime(2025, 1, 1),
                    Note = "Nhà cung cấp nguyên liệu chính"
                },
                new Supplier
                {
                    SupplierId = 2,
                    Code = "SUP002",
                    Name = "Nhà cung cấp B",
                    Address = "TP HCM",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1),
                    UpdatedAt = new DateTime(2025, 1, 1),
                    Note = "Nhà cung cấp sữa và kem"
                },
                new Supplier
                {
                    SupplierId = 3,
                    Code = "SUP003",
                    Name = "Nhà cung cấp C",
                    Address = "Đồng Nai",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1),
                    UpdatedAt = new DateTime(2025, 1, 1),
                    Note = "Nhà cung cấp cà phê"
                },
                new Supplier
                {
                    SupplierId = 4,
                    Code = "SUP004",
                    Name = "Nhà cung cấp D",
                    Address = "Hà Nội",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1),
                    UpdatedAt = new DateTime(2025, 1, 1),
                    Note = "Nhà cung cấp syrup và trà"
                },
                new Supplier
                {
                    SupplierId = 5,
                    Code = "SUP005",
                    Name = "Nhà cung cấp E",
                    Address = "Đà Nẵng",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1),
                    UpdatedAt = new DateTime(2025, 1, 1),
                    Note = "Nhà cung cấp matcha"
                }
            );
        }
    }
}
