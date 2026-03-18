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

            entity.HasKey(x => x.DriId);

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
                .HasForeignKey(x => x.CatId)
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

            entity.HasKey(x => x.CatId);

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

            entity.HasKey(x => x.DriIId);

            entity.Property(x => x.ImageUrl)
                .IsRequired()
                .HasMaxLength(500);

            entity.HasOne(x => x.Drink)
                .WithMany(x => x.DrinkImages)
                .HasForeignKey(x => x.DriId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    // ========================== DRINK SIZE ==========================
    public class DrinkSizeConfiguration : IEntityTypeConfiguration<DrinkSize>
    {
        public void Configure(EntityTypeBuilder<DrinkSize> entity)
        {
            entity.ToTable("DrinkSizes");

            entity.HasKey(x => x.DriSId);

            entity.Property(x => x.Price)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasOne(x => x.Drink)
                .WithMany(x => x.DrinkSizes)
                .HasForeignKey(x => x.DriId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Size)
                .WithMany(x => x.DrinkSizes)
                .HasForeignKey(x => x.SizId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.DriId, x.SizId })
                .IsUnique();
        }
    }

    // ========================== DRINK TOPPING ==========================
    public class DrinkToppingConfiguration : IEntityTypeConfiguration<DrinkTopping>
    {
        public void Configure(EntityTypeBuilder<DrinkTopping> entity)
        {
            entity.ToTable("DrinkToppings");

            entity.HasKey(x => x.DriTId);

            entity.HasOne(x => x.Drink)
                .WithMany(x => x.DrinkToppings)
                .HasForeignKey(x => x.DriId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Topping)
                .WithMany(x => x.DrinkToppings)
                .HasForeignKey(x => x.TopId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.DriId, x.TopId })
                .IsUnique();
        }
    }

    // ========================== SIZE ==========================
    public class SizeConfiguration : IEntityTypeConfiguration<Size>
    {
        public void Configure(EntityTypeBuilder<Size> entity)
        {
            entity.ToTable("Sizes");

            entity.HasKey(x => x.SizId);

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

            entity.HasKey(x => x.TopId);

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

            entity.HasKey(x => x.RecId);

            entity.HasOne(x => x.Drink)
                .WithMany(x => x.Recipes)
                .HasForeignKey(x => x.DriId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.DriId)
                .IsUnique();
        }
    }

    // ========================== RECIPE DETAIL ==========================
    public class RecipeDetailConfiguration : IEntityTypeConfiguration<RecipeDetail>
    {
        public void Configure(EntityTypeBuilder<RecipeDetail> entity)
        {
            entity.ToTable("RecipeDetails");

            entity.HasKey(x => x.RecDId);

            entity.Property(x => x.Quantity)
                .HasColumnType("decimal(18,3)");

            entity.HasOne(x => x.Recipe)
                .WithMany(x => x.RecipeDetails)
                .HasForeignKey(x => x.RecId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Ingredient)
                .WithMany()
                .HasForeignKey(x => x.IngId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.RecId, x.IngId })
                .IsUnique();
        }
    }
}