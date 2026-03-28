using CafeChain.Models.Drinks;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories
{
    public class Ingredient
    {
        public int IngredientId { get; set; }
        public string Code { get; set; } // >= 8 ký tự (VD: ING00001)
        public string Name { get; set; }
        public string BaseUnit { get; set; } // gram, ml, piece
        public bool Active { get; set; }

        public virtual ICollection<RecipeDetail> RecipeDetails { get; set; }
        public virtual ICollection<StoreInventory> StoreInventories { get; set; }
        public virtual ICollection<StockImportDetail> StockImportDetails { get; set; }
    }
}
