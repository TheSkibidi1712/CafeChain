using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Drinks.Toppings
{
    public class DrinkToppingConfiguration : IEntityTypeConfiguration<DrinkTopping>
    {
        public void Configure(EntityTypeBuilder<DrinkTopping> entity)
        {
            entity.ToTable("DrinkToppings");

            entity.HasKey(x => x.DrinkToppingId);

            // Bỏ config Active vì thuộc tính này không có trong DB và đã Set NotMapped


            entity.HasOne(x => x.Drink)
                .WithMany(x => x.DrinkToppings)
                .HasForeignKey(x => x.DrinkId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Topping)
                .WithMany(x => x.DrinkToppings)
                .HasForeignKey(x => x.ToppingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.DrinkId, x.ToppingId })
                .IsUnique();

            entity.HasData(
                new DrinkTopping { DrinkToppingId = 1, DrinkId = 3, ToppingId = 1 },
                new DrinkTopping { DrinkToppingId = 2, DrinkId = 3, ToppingId = 2 },
                new DrinkTopping { DrinkToppingId = 3, DrinkId = 3, ToppingId = 3 },
                new DrinkTopping { DrinkToppingId = 4, DrinkId = 3, ToppingId = 4 },
                new DrinkTopping { DrinkToppingId = 5, DrinkId = 3, ToppingId = 5 },
                new DrinkTopping { DrinkToppingId = 6, DrinkId = 3, ToppingId = 6 },

                new DrinkTopping { DrinkToppingId = 7, DrinkId = 4, ToppingId = 1 },
                new DrinkTopping { DrinkToppingId = 8, DrinkId = 4, ToppingId = 2 },
                new DrinkTopping { DrinkToppingId = 9, DrinkId = 4, ToppingId = 3 },
                new DrinkTopping { DrinkToppingId = 10, DrinkId = 4, ToppingId = 4 },
                new DrinkTopping { DrinkToppingId = 11, DrinkId = 4, ToppingId = 5 },
                new DrinkTopping { DrinkToppingId = 12, DrinkId = 4, ToppingId = 6 }
            );
        }
    }

}
