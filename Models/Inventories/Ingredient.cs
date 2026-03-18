using CafeChain.Models.Drinks;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories
{
    public class Ingredient
    {
        public int IngId { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public bool Active { get; set; }

        public virtual ICollection<RecipeDetail> RecipeDetails { get; set; }
        public virtual ICollection<StoreInventory> StoreInventories { get; set; }
        public virtual ICollection<StockImportDetail> StockImportDetails { get; set; }
    }
}
