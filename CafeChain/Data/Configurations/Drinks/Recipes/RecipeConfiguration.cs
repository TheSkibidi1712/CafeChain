using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Drinks.Recipes
{
    public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
    {
        public void Configure(EntityTypeBuilder<Recipe> entity)
        {
            entity.ToTable("Recipes");

            entity.HasKey(x => x.RecipeId);

            entity.Property(x => x.Name)
                .HasMaxLength(200);

            entity.Property(x => x.YieldPercentage)
                .HasDefaultValue(100);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.Property(x => x.RecipeCode)
                .IsRequired()
                .HasMaxLength(50);

            // === VERSIONING COLUMNS ===
            entity.Property(x => x.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Active");

            entity.Property(x => x.EffectiveDate);

            // === SELF-REFERENCING FK: Recipe → ParentVersion ===
            entity.HasOne(x => x.ParentVersion)
                .WithMany(x => x.ChildVersions)
                .HasForeignKey(x => x.ParentVersionId)
                .HasConstraintName("FK_Recipe_ParentVersion")
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Drink>()
                .WithMany(d => d.Recipes)
                .HasForeignKey(x => x.DrinkId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Size)
                .WithMany()
                .HasForeignKey(x => x.SizeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Topping>()
                .WithMany()
                .HasForeignKey(x => x.ToppingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.RecipeCode);
            entity.HasIndex(x => new { x.DrinkId, x.SizeId });
            entity.HasIndex(x => x.ToppingId);

            entity.HasData(
                new Recipe { RecipeId = 1, RecipeCode = "RCP_CF_SUA", Name = "Recipe CF Sữa", Active = true, Status = "Active", DrinkId = 1, SizeId = 1 },
                new Recipe { RecipeId = 2, RecipeCode = "RCP_CF_DEN", Name = "Recipe CF Đen", Active = true, Status = "Active", DrinkId = 2, SizeId = 1 },
                new Recipe { RecipeId = 3, RecipeCode = "RCP_TS", Name = "Recipe Trà sữa", Active = true, Status = "Active", DrinkId = 3, SizeId = 1 },
                new Recipe { RecipeId = 4, RecipeCode = "RCP_TS_SOCOLA", Name = "Recipe Trà sữa socola", Active = true, Status = "Active", DrinkId = 4, SizeId = 1 },
                new Recipe { RecipeId = 5, RecipeCode = "RCP_TC_DEN", Name = "Trân châu đen", Active = true, Status = "Active", ToppingId = 1 },
                new Recipe { RecipeId = 6, RecipeCode = "RCP_TC_TRANG", Name = "Trân châu trắng", Active = true, Status = "Active", ToppingId = 2 }
            );
        }
    }
}
