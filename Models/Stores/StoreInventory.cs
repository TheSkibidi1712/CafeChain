using CafeChain.Models.Inventories;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Models.Stores
{
    public class StoreInventory
    {
        public int StoreInventoryId { get; set; }
        public int StoreId { get; set; }
        public int IngredientId { get; set; }
        public decimal AvailableQty { get; set; }
        public decimal ReservedQty { get; set; }
        public DateTime LastUpdated { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; }
        public virtual Store Store { get; set; }
        public virtual Ingredient Ingredient { get; set; }

        public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    }
}
