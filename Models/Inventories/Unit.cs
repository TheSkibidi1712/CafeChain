using CafeChain.Models.Drinks;
namespace CafeChain.Models.Inventories
{
    public class Unit
    {
        public int UnitId { get; set; }

        public string UnitCode { get; set; } = null!; // kg, g, ml
        public string Name { get; set; } = null!;     // Kilogram, Gram

        public bool Active { get; set; }

        public virtual ICollection<UnitConversion> FromConversions { get; set; } = new List<UnitConversion>();
        public virtual ICollection<Ingredient> Ingredients { get; set; } = new List<Ingredient>();
        public virtual ICollection<UnitConversion> ToConversions { get; set; } = new List<UnitConversion>();
        public virtual ICollection<RecipeDetail> RecipeDetails { get; set; } = new List<RecipeDetail>();
    }
}
