using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Drinks.Sizes
{
    public class DrinkSizeConfiguration : IEntityTypeConfiguration<DrinkSize>
    {
        public void Configure(EntityTypeBuilder<DrinkSize> entity)
        {
            entity.ToTable("DrinkSizes");

            entity.HasKey(x => x.DrinkSizeId);

            entity.Property(x => x.Price)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.Property(x => x.UpdatedAtUtc)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.Property(x => x.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasOne(x => x.Drink)
                .WithMany(x => x.DrinkSizes)
                .HasForeignKey(x => x.DrinkId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Size)
                .WithMany(x => x.DrinkSizes)
                .HasForeignKey(x => x.SizeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.DrinkId, x.SizeId })
                .IsUnique();

            entity.HasData(
                new DrinkSize { DrinkSizeId = 1, DrinkId = 1, SizeId = 1, Price = 30000m, Active = true },
                new DrinkSize { DrinkSizeId = 3, DrinkId = 2, SizeId = 1, Price = 22000m, Active = true },
                new DrinkSize { DrinkSizeId = 5, DrinkId = 3, SizeId = 1, Price = 22000m, Active = true },
                new DrinkSize { DrinkSizeId = 6, DrinkId = 3, SizeId = 2, Price = 27000m, Active = true },
                new DrinkSize { DrinkSizeId = 7, DrinkId = 3, SizeId = 3, Price = 32000m, Active = true },
                new DrinkSize { DrinkSizeId = 8, DrinkId = 4, SizeId = 1, Price = 25000m, Active = true },
                new DrinkSize { DrinkSizeId = 9, DrinkId = 4, SizeId = 2, Price = 30000m, Active = true },
                new DrinkSize { DrinkSizeId = 10, DrinkId = 4, SizeId = 3, Price = 35000m, Active = true },
                new DrinkSize { DrinkSizeId = 11, DrinkId = 5, SizeId = 5, Price = 15000m, Active = true },
                new DrinkSize { DrinkSizeId = 12, DrinkId = 5, SizeId = 6, Price = 20000m, Active = true },
                new DrinkSize { DrinkSizeId = 13, DrinkId = 5, SizeId = 7, Price = 25000m, Active = true },
                new DrinkSize { DrinkSizeId = 14, DrinkId = 6, SizeId = 5, Price = 15000m, Active = true },
                new DrinkSize { DrinkSizeId = 15, DrinkId = 6, SizeId = 6, Price = 20000m, Active = true },
                new DrinkSize { DrinkSizeId = 16, DrinkId = 6, SizeId = 7, Price = 25000m, Active = true },
                new DrinkSize { DrinkSizeId = 17, DrinkId = 6, SizeId = 8, Price = 30000m, Active = true }

            );
        }
    }
}
