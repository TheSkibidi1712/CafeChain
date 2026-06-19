using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Documents;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Inventories.Transfers;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories.Ingredients
{
    public class Ingredient
    {
        public int IngredientId { get; set; }

        public string Code { get; set; }

        public string Name { get; set; }

        public int BaseUnitId { get; set; }

        public bool Active { get; set; }

        public virtual Unit BaseUnit { get; set; }

        public virtual ICollection<IngredientSupplier> IngredientSuppliers { get; set; } = new List<IngredientSupplier>();

        public virtual ICollection<RecipeDetail> RecipeDetails { get; set; } = new List<RecipeDetail>();

        public virtual ICollection<UnitConversion> UnitConversions { get; set; } = new List<UnitConversion>();

        public virtual ICollection<StoreInventory> StoreInventories { get; set; } = new List<StoreInventory>();

        public virtual ICollection<InventoryDocumentDetail> InventoryDocumentDetails { get; set; } = new List<InventoryDocumentDetail>();

        public virtual ICollection<InventoryTransferDetail> InventoryTransferDetails { get; set; } = new List<InventoryTransferDetail>();
    }
}