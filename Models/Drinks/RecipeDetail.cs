using CafeChain.Models.Inventories;
using System.ComponentModel.DataAnnotations.Schema;

namespace CafeChain.Models.Drinks
{
    public class RecipeDetail
    {
        public int RecipeDetailId { get; set; }

        public int RecipeId { get; set; }

        // 1 trong 2 (QUAN TRỌNG)
        public int? IngredientId { get; set; }
        public int? ChildRecipeId { get; set; }

        public decimal Quantity { get; set; }

        public int UnitId { get; set; }
        // kg, gram, ml...

        // Navigation
        public virtual Unit Unit { get; set; }
        public virtual Recipe Recipe { get; set; }
        public virtual Ingredient Ingredient { get; set; }
        public virtual Recipe ChildRecipe { get; set; }
    }
}
