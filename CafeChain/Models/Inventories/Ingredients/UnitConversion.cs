namespace CafeChain.Models.Inventories.Ingredients
{
    public class UnitConversion
    {
        public int UnitConversionId { get; set; }

        public int IngredientId { get; set; }

        // ===== FROM =====

        public int FromUnitId { get; set; }

        public decimal FromQuantity { get; set; }

        // ===== TO =====

        public int ToUnitId { get; set; }

        public decimal ToQuantity { get; set; }

        public bool Active { get; set; }

        // ===== NAVIGATION =====

        public virtual Ingredient Ingredient { get; set; } = null!;

        public virtual Unit FromUnit { get; set; } = null!;

        public virtual Unit ToUnit { get; set; } = null!;
    }
}