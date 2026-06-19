using CafeChain.Models.Inventories.Ingredients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Ingredients
{
    public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
    {
        public void Configure(EntityTypeBuilder<Ingredient> entity)
        {
            entity.ToTable("Ingredients");

            entity.HasKey(x => x.IngredientId);

            // ================= PROPERTY =================

            entity.Property(x => x.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            // ================= INDEX =================

            entity.HasIndex(x => x.Code)
                .IsUnique();

            entity.HasIndex(x => x.Name);

            entity.HasIndex(x => x.BaseUnitId);

            // ================= RELATION =================

            entity.HasOne(x => x.BaseUnit)
                .WithMany(x => x.Ingredients)
                .HasForeignKey(x => x.BaseUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.InventoryDocumentDetails)
                .WithOne(x => x.Ingredient)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.UnitConversions)
                .WithOne(x => x.Ingredient)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);

            // ================= SEED =================
            entity.HasData(
                new Ingredient { IngredientId = 1, Code = "ING00001", Name = "Cà phê hạt Robusta 1kg", BaseUnitId = 1, Active = true },
                new Ingredient { IngredientId = 2, Code = "ING00002", Name = "Sữa đặc Ông Thọ Vinamilk 380g", BaseUnitId = 3, Active = true },
                new Ingredient { IngredientId = 3, Code = "ING00003", Name = "Trà đen Lipton hộp 100 túi", BaseUnitId = 1, Active = true },
                new Ingredient { IngredientId = 4, Code = "ING00004", Name = "Bột sữa B-One 1kg", BaseUnitId = 1, Active = true },
                new Ingredient { IngredientId = 5, Code = "ING00005", Name = "Bột cacao Van Houten 1kg", BaseUnitId = 1, Active = true },
                new Ingredient { IngredientId = 6, Code = "ING00006", Name = "Đường trắng Biên Hòa 1kg", BaseUnitId = 1, Active = true },
                new Ingredient { IngredientId = 7, Code = "ING00007", Name = "Đá viên 1kg", BaseUnitId = 1, Active = true },
                new Ingredient { IngredientId = 8, Code = "ING00008", Name = "Syrup Torani Vanilla 750ml", BaseUnitId = 3, Active = true },
                new Ingredient { IngredientId = 9, Code = "ING00009", Name = "Matcha Nhật Bản 500g", BaseUnitId = 1, Active = true },
                new Ingredient { IngredientId = 10, Code = "ING00010", Name = "Kem béo Rich's 1L", BaseUnitId = 3, Active = true },
                new Ingredient { IngredientId = 11, Code = "ING00011", Name = "Bột năng Vĩnh Thuận 400g", BaseUnitId = 1, Active = true },
                new Ingredient { IngredientId = 12, Code = "ING00012", Name = "Đường nâu Hàn Quốc 1kg", BaseUnitId = 1, Active = true },
                new Ingredient { IngredientId = 13, Code = "ING00013", Name = "Nước lọc Lavie 500ml", BaseUnitId = 3, Active = true }
            );
        }
    }
}
