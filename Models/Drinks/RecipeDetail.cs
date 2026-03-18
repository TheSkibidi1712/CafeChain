using CafeChain.Models.Inventories;

namespace CafeChain.Models.Drinks
{
    public class RecipeDetail
    {
        public int RecDId { get; set; }
        public int RecId { get; set; }
        public int IngId { get; set; }
        public decimal Quantity { get; set; }

        public virtual Recipe Recipe { get; set; }
        public virtual Ingredient Ingredient { get; set; }
    }
}
