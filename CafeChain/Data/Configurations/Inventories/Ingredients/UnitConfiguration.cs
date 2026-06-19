using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Ingredients
{
    public class UnitConfiguration : IEntityTypeConfiguration<Unit>
    {
        public void Configure(EntityTypeBuilder<Unit> entity)
        {
            entity.ToTable("Units");

            entity.HasKey(x => x.UnitId);

            // ================= PROPERTY =================

            entity.Property(x => x.UnitCode)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(x => x.Type)
                .IsRequired();

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            // ================= INDEX =================

            entity.HasIndex(x => x.UnitCode)
                .IsUnique();

            entity.HasIndex(x => x.Name);

            entity.HasIndex(x => x.Type);

            // ================= SEED =================

            entity.HasData(
                new Unit { UnitId = 1, UnitCode = "g", Name = "Gram", Type = UnitType.KhoiLuong, Active = true },
                new Unit { UnitId = 2, UnitCode = "kg", Name = "Kilogram", Type = UnitType.KhoiLuong, Active = true },

                new Unit { UnitId = 3, UnitCode = "ml", Name = "Milliliter", Type = UnitType.TheTich, Active = true },
                new Unit { UnitId = 4, UnitCode = "l", Name = "Liter", Type = UnitType.TheTich, Active = true },
                new Unit { UnitId = 5, UnitCode = "oz", Name = "Ounce", Type = UnitType.TheTich, Active = true },
                new Unit { UnitId = 6, UnitCode = "cup", Name = "Cup", Type = UnitType.TheTich, Active = true },
                new Unit { UnitId = 7, UnitCode = "tbsp", Name = "Tablespoon", Type = UnitType.TheTich, Active = true },
                new Unit { UnitId = 8, UnitCode = "tsp", Name = "Teaspoon", Type = UnitType.TheTich, Active = true },

                new Unit { UnitId = 9, UnitCode = "pcs", Name = "Piece", Type = UnitType.Dem, Active = true },
                new Unit { UnitId = 10, UnitCode = "bottle", Name = "Bottle", Type = UnitType.Dem, Active = true },
                new Unit { UnitId = 11, UnitCode = "can", Name = "Can", Type = UnitType.Dem, Active = true },
                new Unit { UnitId = 12, UnitCode = "pack", Name = "Pack", Type = UnitType.Dem, Active = true }
            );
        }
    }
}
