using System.ComponentModel.DataAnnotations.Schema;

namespace CafeChain.Models.Drinks
{
    public class Recipe
    {
        public int RecipeId { get; set; }

        public string Name { get; set; }

        public decimal YieldPercentage { get; set; } = 100;
        // hao hụt: 95% nghĩa là mất 5%

        public bool Active { get; set; }
        
        // Relationships for inventory lookup
        public int? DrinkId { get; set; }
        public int? ToppingId { get; set; }

        public virtual ICollection<RecipeDetail> RecipeDetails { get; set; }

        public virtual ICollection<RecipeDetail> ChildRecipeDetails { get; set; }     // dùng ChildRecipeId

    }
}
