namespace CafeChain.Models.Drinks
{
    public class Recipe
    {
        public int RecipeId { get; set; }

        public string Name { get; set; }

        public decimal YieldPercentage { get; set; } = 100;
        // hao hụt: 95% nghĩa là mất 5%

        public bool Active { get; set; }

        public virtual ICollection<RecipeDetail> RecipeDetails { get; set; }
    }
}
