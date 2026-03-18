using CafeChain.Models.Inventories;

namespace CafeChain.Models.Drinks
{
    public class RecipeDetail
    {
        public int RecipeDetailId { get; set; }
        public int RecipeId { get; set; }
        public int IngredientId { get; set; }
        public decimal Quantity { get; set; }

        public virtual Recipe Recipe { get; set; }
        public virtual Ingredient Ingredient { get; set; }
    }
}
