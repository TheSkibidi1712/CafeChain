using System.ComponentModel.DataAnnotations.Schema;

namespace CafeChain.Models.Inventories
{
    public class UnitConversion
    {
        public int UnitConversionId { get; set; }

        public int IngredientId { get; set; }

        // 🔹 Đơn vị nguồn
        public int FromUnitId { get; set; }
        public decimal FromQuantity { get; set; } // ví dụ: 1

        // 🔹 Đơn vị đích
        public int ToUnitId { get; set; }
        public decimal ToQuantity { get; set; }   // ví dụ: 1000

        // Navigation
        public virtual Ingredient Ingredient { get; set; } = null!;

        public virtual Unit FromUnit { get; set; } = null!;

        public virtual Unit ToUnit { get; set; } = null!;
    }
}
