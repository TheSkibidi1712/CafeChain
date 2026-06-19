using CafeChain.Models.Inventories.Ingredients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Inventories.Ingredients
{
    public class UnitConversionConfiguration : IEntityTypeConfiguration<UnitConversion>
    {
        public void Configure(EntityTypeBuilder<UnitConversion> entity)
        {
            entity.ToTable("UnitConversions", table =>
            {
                table.HasCheckConstraint(
                    "CK_UnitConversion_NotSameUnit",
                    "[FromUnitId] <> [ToUnitId]"
                );

                table.HasCheckConstraint(
                    "CK_UnitConversion_PositiveQty",
                    "[FromQuantity] > 0 AND [ToQuantity] > 0"
                );
            });

            entity.HasKey(x => x.UnitConversionId);

            // ================= PROPERTY =================

            entity.Property(x => x.FromQuantity)
                .HasColumnType("decimal(18,5)")
                .IsRequired();

            entity.Property(x => x.ToQuantity)
                .HasColumnType("decimal(18,5)")
                .IsRequired();

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            // ================= RELATION =================

            entity.HasOne(x => x.Ingredient)
                .WithMany(i => i.UnitConversions)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.FromUnit)
                .WithMany(u => u.FromConversions)
                .HasForeignKey(x => x.FromUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ToUnit)
                .WithMany(u => u.ToConversions)
                .HasForeignKey(x => x.ToUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            entity.HasIndex(x => new
            {
                x.IngredientId,
                x.FromUnitId,
                x.ToUnitId
            }).IsUnique();

            entity.HasIndex(x => x.IngredientId);


            // ================= SEED =================
            entity.HasData(

               // ================= MASS (kg -> g) =================
               new UnitConversion { UnitConversionId = 1, IngredientId = 1, FromUnitId = 2, FromQuantity = 1, ToUnitId = 1, ToQuantity = 1000 },
               new UnitConversion { UnitConversionId = 2, IngredientId = 3, FromUnitId = 2, FromQuantity = 1, ToUnitId = 1, ToQuantity = 1000 },
               new UnitConversion { UnitConversionId = 3, IngredientId = 4, FromUnitId = 2, FromQuantity = 1, ToUnitId = 1, ToQuantity = 1000 },
               new UnitConversion { UnitConversionId = 4, IngredientId = 5, FromUnitId = 2, FromQuantity = 1, ToUnitId = 1, ToQuantity = 1000 },
               new UnitConversion { UnitConversionId = 5, IngredientId = 6, FromUnitId = 2, FromQuantity = 1, ToUnitId = 1, ToQuantity = 1000 },
               new UnitConversion { UnitConversionId = 6, IngredientId = 7, FromUnitId = 2, FromQuantity = 1, ToUnitId = 1, ToQuantity = 1000 },
               new UnitConversion { UnitConversionId = 7, IngredientId = 9, FromUnitId = 2, FromQuantity = 1, ToUnitId = 1, ToQuantity = 1000 },
               new UnitConversion { UnitConversionId = 8, IngredientId = 11, FromUnitId = 2, FromQuantity = 1, ToUnitId = 1, ToQuantity = 1000 },
               new UnitConversion { UnitConversionId = 9, IngredientId = 12, FromUnitId = 2, FromQuantity = 1, ToUnitId = 1, ToQuantity = 1000 },

               // ================= VOLUME (l -> ml) =================
               new UnitConversion { UnitConversionId = 20, IngredientId = 2, FromUnitId = 4, FromQuantity = 1, ToUnitId = 3, ToQuantity = 1000 },
               new UnitConversion { UnitConversionId = 21, IngredientId = 8, FromUnitId = 4, FromQuantity = 1, ToUnitId = 3, ToQuantity = 1000 },
               new UnitConversion { UnitConversionId = 22, IngredientId = 10, FromUnitId = 4, FromQuantity = 1, ToUnitId = 3, ToQuantity = 1000 },
               new UnitConversion { UnitConversionId = 23, IngredientId = 13, FromUnitId = 4, FromQuantity = 1, ToUnitId = 3, ToQuantity = 1000 },

               // ================= oz =================
               new UnitConversion { UnitConversionId = 30, IngredientId = 2, FromUnitId = 5, FromQuantity = 1, ToUnitId = 3, ToQuantity = 29.5735m },
               new UnitConversion { UnitConversionId = 31, IngredientId = 8, FromUnitId = 5, FromQuantity = 1, ToUnitId = 3, ToQuantity = 29.5735m },
               new UnitConversion { UnitConversionId = 32, IngredientId = 10, FromUnitId = 5, FromQuantity = 1, ToUnitId = 3, ToQuantity = 29.5735m },

               // ================= cup =================
               new UnitConversion { UnitConversionId = 40, IngredientId = 2, FromUnitId = 6, FromQuantity = 1, ToUnitId = 3, ToQuantity = 240 },
               new UnitConversion { UnitConversionId = 41, IngredientId = 8, FromUnitId = 6, FromQuantity = 1, ToUnitId = 3, ToQuantity = 240 },
               new UnitConversion { UnitConversionId = 42, IngredientId = 10, FromUnitId = 6, FromQuantity = 1, ToUnitId = 3, ToQuantity = 240 },

               // ================= tbsp =================
               new UnitConversion { UnitConversionId = 50, IngredientId = 2, FromUnitId = 7, FromQuantity = 1, ToUnitId = 3, ToQuantity = 15 },

               // ================= tsp =================
               new UnitConversion { UnitConversionId = 60, IngredientId = 2, FromUnitId = 8, FromQuantity = 1, ToUnitId = 3, ToQuantity = 5 },

               // ================= COUNT (quan trọng nhất) =================

               // Syrup Torani (750ml)
               new UnitConversion { UnitConversionId = 70, IngredientId = 8, FromUnitId = 10, FromQuantity = 1, ToUnitId = 3, ToQuantity = 750 },

               // Sữa đặc (1 lon ~ 300ml)
               new UnitConversion { UnitConversionId = 71, IngredientId = 2, FromUnitId = 11, FromQuantity = 1, ToUnitId = 3, ToQuantity = 300 },

               // Nước Lavie (1 chai = 500ml)
               new UnitConversion { UnitConversionId = 72, IngredientId = 13, FromUnitId = 11, FromQuantity = 1, ToUnitId = 3, ToQuantity = 500 }
           );
        }
    }
}
