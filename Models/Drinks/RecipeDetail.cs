using CafeChain.Models.Inventories;

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

        public string Unit { get; set; }
        // kg, gram, ml...

        // Navigation
        public virtual Recipe Recipe { get; set; }
        public virtual Ingredient Ingredient { get; set; }
        public virtual Recipe ChildRecipe { get; set; }
    }
}
