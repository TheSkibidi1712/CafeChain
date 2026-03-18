namespace CafeChain.Models.Drinks
{
    public class Recipe
    {
        public int RecipeId { get; set; }
        public int DrinkId { get; set; }

        public virtual Drink Drink { get; set; }
        public virtual ICollection<RecipeDetail> RecipeDetails { get; set; }
    }
}
