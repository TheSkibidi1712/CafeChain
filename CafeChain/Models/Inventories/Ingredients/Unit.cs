using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Documents;

namespace CafeChain.Models.Inventories.Ingredients
{
    public class Unit
    {
        public int UnitId { get; set; }

        public string UnitCode { get; set; } = null!;

        public string Name { get; set; } = null!;

        public UnitType Type { get; set; }

        public bool Active { get; set; }

        // ===== CONVERSION =====

        public virtual ICollection<UnitConversion> FromConversions { get; set; }
            = new List<UnitConversion>();

        public virtual ICollection<UnitConversion> ToConversions { get; set; }
            = new List<UnitConversion>();

        // ===== INVENTORY =====

        public virtual ICollection<InventoryDocumentDetail> InventoryDocumentDetails { get; set; }
            = new List<InventoryDocumentDetail>();

        public virtual ICollection<Ingredient> Ingredients { get; set; }
            = new List<Ingredient>();

        public virtual ICollection<RecipeDetail> RecipeDetails { get; set; }
            = new List<RecipeDetail>();
    }
}