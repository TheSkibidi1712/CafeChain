using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Drinks.Recipes
{
    public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
    {
        public void Configure(EntityTypeBuilder<Recipe> entity)
        {
            entity.ToTable("Recipes", t =>
            {
                t.HasCheckConstraint(
                    "CK_Recipes_OutputQuantity_Positive",
                    "[OutputQuantity] IS NULL OR [OutputQuantity] > 0");
                t.HasCheckConstraint(
                    "CK_Recipes_YieldVarianceTolerance",
                    "[YieldVarianceTolerancePercent] IS NULL OR " +
                    "([YieldVarianceTolerancePercent] >= 0 AND [YieldVarianceTolerancePercent] <= 100)");

                // BTP output fields are all-or-none (nullable together for POS/topping/legacy).
                t.HasCheckConstraint(
                    "CK_Recipes_PreparedItemOutput_AllOrNone",
                    @"([PreparedItemId] IS NULL AND [OutputQuantity] IS NULL AND [OutputUnitId] IS NULL)
                    OR ([PreparedItemId] IS NOT NULL AND [OutputQuantity] IS NOT NULL AND [OutputQuantity] > 0 AND [OutputUnitId] IS NOT NULL)");
            });

            entity.HasKey(x => x.RecipeId);

            entity.Property(x => x.Name)
                .HasMaxLength(200);

            entity.Property(x => x.YieldPercentage)
                .HasPrecision(18, 2)
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

            // === #112 BTP output contract ===
            entity.Property(x => x.OutputQuantity)
                .HasColumnType("decimal(18,5)");
            entity.Property(x => x.YieldVarianceTolerancePercent)
                .HasColumnType("decimal(9,4)");

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

            entity.HasOne(x => x.PreparedItem)
                .WithMany()
                .HasForeignKey(x => x.PreparedItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OutputUnit)
                .WithMany()
                .HasForeignKey(x => x.OutputUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.RecipeCode);
            entity.HasIndex(x => new { x.DrinkId, x.SizeId });
            entity.HasIndex(x => new { x.DrinkId, x.SizeId })
                .IsUnique()
                .HasFilter("[DrinkId] IS NOT NULL AND [SizeId] IS NOT NULL AND [ToppingId] IS NULL AND [Active] = 1 AND [Status] = 'Active'")
                .HasDatabaseName("UX_Recipes_OneActive_Drink_Size");
            entity.HasIndex(x => x.ToppingId);
            entity.HasIndex(x => x.PreparedItemId);
            entity.HasIndex(x => x.OutputUnitId);

            // One Active Recipe version per PreparedItem (operational uniqueness).
            entity.HasIndex(x => x.PreparedItemId)
                .IsUnique()
                .HasFilter("[PreparedItemId] IS NOT NULL AND [Active] = 1")
                .HasDatabaseName("IX_Recipes_OneActive_PreparedItem");

            // No PreparedItem / Recipe output seed mappings in #112.
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
