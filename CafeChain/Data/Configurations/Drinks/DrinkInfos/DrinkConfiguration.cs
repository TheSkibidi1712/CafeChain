using CafeChain.Models;
using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Drinks.DrinkInfos
{
    public class DrinkConfiguration : IEntityTypeConfiguration<Drink>
    {
        public void Configure(EntityTypeBuilder<Drink> entity)
        {
            entity.ToTable("Drinks");

            entity.HasKey(x => x.DrinkId);

            // ================= PROPERTIES =================
            entity.Property(x => x.DrinkCode)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Description)
                .HasMaxLength(1000);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.Property(x => x.CalculatedCogs)
                .HasPrecision(18, 2)
                .IsRequired(false); // cho phép null

            // ================= RELATIONSHIPS =================

            // Category
            entity.HasOne(x => x.Category)
                .WithMany(x => x.Drinks)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            // ProductType (🔥 thiếu trong config cũ)
            entity.HasOne(x => x.ProductType)
                .WithMany(x => x.Drinks)
                .HasForeignKey(x => x.ProductTypeId)
                .OnDelete(DeleteBehavior.Restrict);


            // ================= INDEX =================

            entity.HasIndex(x => x.Name).IsUnique();

            entity.HasIndex(x => x.DrinkCode).IsUnique();

            entity.HasIndex(x => new { x.CategoryId, x.ProductTypeId });

            // ================= SEED DATA =================
            entity.HasData(
                new Drink
                {
                    DrinkId = 1,
                    CategoryId = 1,
                    DrinkCode = "CF_Sua",
                    ProductTypeId = 1,
                    Name = "Cà phê sữa",
                    Description = "Cà phê pha với sữa đặc.",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1),
                    CalculatedCogs = 0 // Giá vốn sẽ được tính tự động từ BOM, nên để 0 khi seed
                },
                new Drink
                {
                    DrinkId = 2,
                    CategoryId = 1,
                    DrinkCode = "CF_Den",
                    ProductTypeId = 1,
                    Name = "Cà phê đen",
                    Description = "Cà phê pha với nước sôi, không có sữa.",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1),
                    CalculatedCogs = 0 // Giá vốn sẽ được tính tự động từ BOM, nên để 0 khi seed
                },
                new Drink
                {
                    DrinkId = 3,
                    CategoryId = 2,
                    DrinkCode = "TS_TruyenThong",
                    ProductTypeId = 1,
                    Name = "Trà sữa truyền thống",
                    Description = "Trà sữa pha với trân châu đen và đá viên.",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1),
                    CalculatedCogs = 0 // Giá vốn sẽ được tính tự động từ BOM, nên để 0 khi seed
                },
                new Drink
                {
                    DrinkId = 4,
                    CategoryId = 2,
                    DrinkCode = "TS_Socola",
                    ProductTypeId = 1,
                    Name = "Trà sữa socola",
                    Description = "Trà sữa socola thơm ngon, béo ngậy.",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1),
                    CalculatedCogs = 0 // Giá vốn sẽ được tính tự động từ BOM, nên để 0 khi seed
                },
                new Drink
                {
                    DrinkId = 5,
                    CategoryId = 3,
                    DrinkCode = "STING",
                    ProductTypeId = 2,
                    Name = "Sting",
                    Description = "Sting mát lạnh",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1),
                    CalculatedCogs = 0 // Giá vốn sẽ được tính tự động từ BOM, nên để 0 khi seed
                },
                new Drink
                {
                    DrinkId = 6,
                    CategoryId = 3,
                    DrinkCode = "COCA",
                    ProductTypeId = 2,
                    Name = "Coca-cola",
                    Description = "Coca-cola mát lạnh",
                    Active = true,
                    CreatedAt = new DateTime(2025, 1, 1),
                    CalculatedCogs = 0 // Giá vốn sẽ được tính tự động từ BOM, nên để 0 khi seed
                }
            );
        }
    } 
}