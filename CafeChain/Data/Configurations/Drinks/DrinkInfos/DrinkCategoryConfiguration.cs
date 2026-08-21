using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Drinks.DrinkInfos
{
    public class DrinkCategoryConfiguration : IEntityTypeConfiguration<DrinkCategory>
    {
        public void Configure(EntityTypeBuilder<DrinkCategory> entity)
        {
            entity.ToTable("DrinkCategories");

            entity.HasKey(x => x.CategoryId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.CategoryCode)
                .IsRequired()
                .HasMaxLength(30);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasIndex(x => x.Name).IsUnique();

            entity.HasIndex(x => x.CategoryCode).IsUnique();

            entity.Property(x => x.Icon)
                .HasMaxLength(10);

            entity.HasData(
                new DrinkCategory { CategoryId = 1, Name = "Coffee", CategoryCode = "COFFEE", Icon = "☕", Active = true },
                new DrinkCategory { CategoryId = 2, Name = "Trà sữa", CategoryCode = "TRASUA", Icon = "🧋", Active = true }
            );
        }
    }
}
