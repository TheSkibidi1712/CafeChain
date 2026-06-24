using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Drinks.Recipes
{
    public class RecipeDetailConfiguration : IEntityTypeConfiguration<RecipeDetail>
    {
        public void Configure(EntityTypeBuilder<RecipeDetail> entity)
        {
            entity.ToTable("RecipeDetails", t =>
            {
                t.HasCheckConstraint(
                    "CK_RecipeDetail_OnlyOneSource",
                    @"(IngredientId IS NOT NULL AND ChildRecipeId IS NULL)
                    OR (IngredientId IS NULL AND ChildRecipeId IS NOT NULL)"
                );
            });

            entity.HasKey(x => x.RecipeDetailId);

            // ================= PROPERTIES =================
            entity.Property(x => x.Quantity)
                .HasColumnType("decimal(18,3)")
                .IsRequired();

            entity.Property(x => x.UnitId)
                .IsRequired();

            // ================= RELATIONSHIPS =================

            // 🔥 Quan hệ chính
            entity.HasOne(x => x.Recipe)
                .WithMany(x => x.RecipeDetails)
                .HasForeignKey(x => x.RecipeId)
                .HasConstraintName("FK_RecipeDetail_Recipe") // 👈 thêm để rõ ràng
                .OnDelete(DeleteBehavior.Cascade);

            // 🔥 Quan hệ self-reference
            entity.HasOne(x => x.ChildRecipe)
                .WithMany(x => x.ChildRecipeDetails)
                .HasForeignKey(x => x.ChildRecipeId)
                .HasConstraintName("FK_RecipeDetail_ChildRecipe") // 👈 QUAN TRỌNG
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Ingredient)
                .WithMany(x => x.RecipeDetails)
                .HasForeignKey(x => x.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Unit)
                .WithMany(x => x.RecipeDetails)
                .HasForeignKey(x => x.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================

            entity.HasIndex(x => new { x.RecipeId, x.IngredientId })
                .IsUnique()
                .HasFilter("[IngredientId] IS NOT NULL");

            entity.HasIndex(x => new { x.RecipeId, x.ChildRecipeId })
                .IsUnique()
                .HasFilter("[ChildRecipeId] IS NOT NULL");

            entity.HasIndex(x => x.UnitId); // 🔥 thêm để optimize

            // ================= SEED =================

            entity.HasData(
                // ===== Recipe 1 =====
                new RecipeDetail { RecipeDetailId = 1, RecipeId = 1, IngredientId = 1, Quantity = 50m, UnitId = 3 }, // ml
                new RecipeDetail { RecipeDetailId = 2, RecipeId = 1, IngredientId = 2, Quantity = 30m, UnitId = 3 },
                new RecipeDetail { RecipeDetailId = 3, RecipeId = 1, IngredientId = 7, Quantity = 100m, UnitId = 3 },

                // ===== Recipe 2 =====
                new RecipeDetail { RecipeDetailId = 4, RecipeId = 2, IngredientId = 1, Quantity = 60m, UnitId = 3 },
                new RecipeDetail { RecipeDetailId = 5, RecipeId = 2, IngredientId = 7, Quantity = 100m, UnitId = 3 },

                // ===== Recipe 3 =====
                new RecipeDetail { RecipeDetailId = 6, RecipeId = 3, IngredientId = 3, Quantity = 80m, UnitId = 3 },
                new RecipeDetail { RecipeDetailId = 7, RecipeId = 3, IngredientId = 4, Quantity = 40m, UnitId = 3 },
                new RecipeDetail { RecipeDetailId = 8, RecipeId = 3, IngredientId = 6, Quantity = 20m, UnitId = 3 },
                new RecipeDetail { RecipeDetailId = 9, RecipeId = 3, IngredientId = 7, Quantity = 100m, UnitId = 3 },

                // ===== Recipe 4 =====
                new RecipeDetail { RecipeDetailId = 10, RecipeId = 4, IngredientId = 3, Quantity = 70m, UnitId = 3 },
                new RecipeDetail { RecipeDetailId = 11, RecipeId = 4, IngredientId = 4, Quantity = 40m, UnitId = 3 },
                new RecipeDetail { RecipeDetailId = 12, RecipeId = 4, IngredientId = 5, Quantity = 20m, UnitId = 3 },
                new RecipeDetail { RecipeDetailId = 13, RecipeId = 4, IngredientId = 6, Quantity = 20m, UnitId = 3 },
                new RecipeDetail { RecipeDetailId = 14, RecipeId = 4, IngredientId = 7, Quantity = 100m, UnitId = 3 },

                // ===== Recipe 5 (g) =====
                new RecipeDetail { RecipeDetailId = 15, RecipeId = 5, IngredientId = 11, Quantity = 100m, UnitId = 1 }, // g
                new RecipeDetail { RecipeDetailId = 16, RecipeId = 5, IngredientId = 12, Quantity = 50m, UnitId = 1 },
                new RecipeDetail { RecipeDetailId = 17, RecipeId = 5, IngredientId = 13, Quantity = 60m, UnitId = 3 },

                // ===== Recipe 6 =====
                new RecipeDetail { RecipeDetailId = 18, RecipeId = 6, IngredientId = 11, Quantity = 100m, UnitId = 1 },
                new RecipeDetail { RecipeDetailId = 19, RecipeId = 6, IngredientId = 6, Quantity = 40m, UnitId = 1 },
                new RecipeDetail { RecipeDetailId = 20, RecipeId = 6, IngredientId = 13, Quantity = 60m, UnitId = 3 },

                // ===== Child recipe =====
                new RecipeDetail
                {
                    RecipeDetailId = 21,
                    RecipeId = 3,
                    ChildRecipeId = 5,
                    Quantity = 1,
                    UnitId = 1 // hoặc tạo unit "portion"
                }
            );
        }
    }
}
