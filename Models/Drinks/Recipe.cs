namespace CafeChain.Models.Drinks
{
    public class Recipe
    {
        public int RecId { get; set; }
        public int DriId { get; set; }

        public virtual Drink Drink { get; set; }
        public virtual ICollection<RecipeDetail> RecipeDetails { get; set; }
    }
}
