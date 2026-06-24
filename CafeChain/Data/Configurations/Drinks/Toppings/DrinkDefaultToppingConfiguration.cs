using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Drinks.Toppings
{
    public class DrinkDefaultToppingConfiguration : IEntityTypeConfiguration<DrinkDefaultTopping>
    {
        public void Configure(EntityTypeBuilder<DrinkDefaultTopping> entity)
        {
            entity.ToTable("DrinkDefaultToppings");

            // ================= KEY =================
            entity.HasKey(x => x.DrinkDefaultToppingId);

            // ================= RELATIONSHIPS =================

            entity.HasOne(x => x.Drink)
                .WithMany(d => d.DrinkDefaultToppings) // 👈 nếu chưa add navigation bên Drink
                .HasForeignKey(x => x.DrinkId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Topping)
                .WithMany() // 👈 nếu chưa add navigation bên Topping
                .HasForeignKey(x => x.ToppingId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            entity.HasIndex(x => new { x.DrinkId, x.ToppingId })
                .IsUnique();

            // ================= SEED DATA =================

            entity.HasData(
                new DrinkDefaultTopping { DrinkDefaultToppingId = 1, DrinkId = 4, ToppingId = 1 },
                new DrinkDefaultTopping { DrinkDefaultToppingId = 2, DrinkId = 4, ToppingId = 2 },
                new DrinkDefaultTopping { DrinkDefaultToppingId = 3, DrinkId = 4, ToppingId = 3 },
                new DrinkDefaultTopping { DrinkDefaultToppingId = 4, DrinkId = 4, ToppingId = 4 },
                new DrinkDefaultTopping { DrinkDefaultToppingId = 5, DrinkId = 4, ToppingId = 5 },
                new DrinkDefaultTopping { DrinkDefaultToppingId = 6, DrinkId = 4, ToppingId = 6 }
            );
        }
    }
}
