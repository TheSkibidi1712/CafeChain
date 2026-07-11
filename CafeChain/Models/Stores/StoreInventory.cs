using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Transactions;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Models.Stores
{
    public class StoreInventory
    {
        public int StoreInventoryId { get; set; }
        public int StoreId { get; set; }
        public int? IngredientId { get; set; }
        public int? RecipeId { get; set; }

        /// <summary>
        /// Additive stable BTP identity for the #115 dual-read period.
        /// RecipeId remains the legacy writer key until the later writer cutover.
        /// </summary>
        public int? PreparedItemId { get; set; }
        public decimal AvailableQty { get; set; }
        public decimal ReservedQty { get; set; }
        public decimal? MaxNegativeQty { get; set; }

        /// <summary>
        /// Issue #97 — optional min stock threshold for LOW_STOCK detection.
        /// Null = “Chưa cấu hình ngưỡng tối thiểu” (no auto LOW_STOCK).
        /// </summary>
        public decimal? MinStockLevel { get; set; }

        public DateTime LastUpdated { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; }
        public virtual Store Store { get; set; }
        public virtual Ingredient Ingredient { get; set; }
        public virtual CafeChain.Models.Drinks.Recipe Recipe { get; set; }
        public virtual PreparedItem? PreparedItem { get; set; }

        public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    }
}
