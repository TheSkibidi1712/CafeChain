using CafeChain.Models;
using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations
{
    // ========================== DRINK ==========================
    public class DrinkConfiguration : IEntityTypeConfiguration<Drink>
    {
        public void Configure(EntityTypeBuilder<Drink> entity)
        {
            entity.ToTable("Drinks");

            entity.HasKey(x => x.DrinkId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Description)
                .HasMaxLength(1000);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.HasOne(x => x.Category)
                .WithMany(x => x.Drinks)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => x.Name)
                .IsUnique();
        }
    }

    // ========================== DRINK CATEGORY ==========================
    public class DrinkCategoryConfiguration : IEntityTypeConfiguration<DrinkCategory>
    {
        public void Configure(EntityTypeBuilder<DrinkCategory> entity)
        {
            entity.ToTable("DrinkCategories");

            entity.HasKey(x => x.CategoryId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasIndex(x => x.Name)
                .IsUnique();
        }
    }

    // ========================== DRINK IMAGE ==========================
    public class DrinkImageConfiguration : IEntityTypeConfiguration<DrinkImage>
    {
        public void Configure(EntityTypeBuilder<DrinkImage> entity)
        {
            entity.ToTable("DrinkImages");

            entity.HasKey(x => x.DrinkImageId);

            entity.Property(x => x.ImageUrl)
                .IsRequired()
                .HasMaxLength(500);

            entity.HasOne(x => x.Drink)
                .WithMany(x => x.DrinkImages)
                .HasForeignKey(x => x.DrinkId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    // ========================== DRINK SIZE ==========================
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
        }
    }

    // ========================== DRINK TOPPING ==========================
    public class DrinkToppingConfiguration : IEntityTypeConfiguration<DrinkTopping>
    {
        public void Configure(EntityTypeBuilder<DrinkTopping> entity)
        {
            entity.ToTable("DrinkToppings");

            entity.HasKey(x => x.DrinkToppingId);

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
        }
    }

    // ========================== SIZE ==========================
    public class SizeConfiguration : IEntityTypeConfiguration<Size>
    {
        public void Configure(EntityTypeBuilder<Size> entity)
        {
            entity.ToTable("Sizes");

            entity.HasKey(x => x.SizeId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.Description)
                .HasMaxLength(300);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasIndex(x => x.Name)
                .IsUnique();
        }
    }

    // ========================== TOPPING ==========================
    public class ToppingConfiguration : IEntityTypeConfiguration<Topping>
    {
        public void Configure(EntityTypeBuilder<Topping> entity)
        {
            entity.ToTable("Toppings");

            entity.HasKey(x => x.ToppingId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(x => x.Price)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasIndex(x => x.Name)
                .IsUnique();
        }
    }

    // ========================== RECIPE ==========================
    public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
    {
        public void Configure(EntityTypeBuilder<Recipe> entity)
        {
            entity.ToTable("Recipes");

            entity.HasKey(x => x.RecipeId);

            entity.HasOne(x => x.Drink)
                .WithMany(x => x.Recipes)
                .HasForeignKey(x => x.DrinkId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.DrinkId)
                .IsUnique();
        }
    }

    // ========================== RECIPE DETAIL ==========================
    public class RecipeDetailConfiguration : IEntityTypeConfiguration<RecipeDetail>
    {
        public void Configure(EntityTypeBuilder<RecipeDetail> entity)
        {
            entity.ToTable("RecipeDetails");

            entity.HasKey(x => x.RecipeDetailId);

            entity.Property(x => x.Quantity)
                .HasColumnType("decimal(18,3)");

            entity.HasOne(x => x.Recipe)
                .WithMany(x => x.RecipeDetails)
                .HasForeignKey(x => x.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Ingredient)
                .WithMany()
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.RecipeId, x.IngredientId })
                .IsUnique();
        }
    }
}