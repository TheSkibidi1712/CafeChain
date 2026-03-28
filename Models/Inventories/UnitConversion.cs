namespace CafeChain.Models.Inventories
{
    public class UnitConversion
    {
        public int UnitConversionId { get; set; }

        public int IngredientId { get; set; }

        public string FromUnit { get; set; } // kg
        public string ToUnit { get; set; }   // gram

        public decimal Ratio { get; set; }

        public virtual Ingredient Ingredient { get; set; }
    }
}
