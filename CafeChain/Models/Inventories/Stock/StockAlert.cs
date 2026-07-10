using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories.Stock
{
    /// <summary>
    /// Issue #97 — persisted LOW_STOCK / OUT_OF_STOCK alert for a store inventory item
    /// (IngredientId XOR RecipeId), following ADR-0004 one-level identity.
    /// </summary>
    public class StockAlert
    {
        public int StockAlertId { get; set; }

        public int StoreId { get; set; }

        public int? IngredientId { get; set; }

        public int? RecipeId { get; set; }

        /// <summary>LOW_STOCK | OUT_OF_STOCK</summary>
        public string AlertType { get; set; } = string.Empty;

        /// <summary>WARNING | URGENT</summary>
        public string Severity { get; set; } = string.Empty;

        /// <summary>OPEN | RESOLVED</summary>
        public string Status { get; set; } = string.Empty;

        public decimal CurrentQtySnapshot { get; set; }

        public decimal? ThresholdSnapshot { get; set; }

        /// <summary>AUTO | MANUAL_CHECK | POS_SALE | OFFLINE_SYNC | INVENTORY_TRANSACTION</summary>
        public string Source { get; set; } = string.Empty;

        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public DateTime? ResolvedAt { get; set; }

        public string? ResolvedReason { get; set; }

        public virtual Store Store { get; set; } = null!;
        public virtual Ingredient? Ingredient { get; set; }
        public virtual Recipe? Recipe { get; set; }
    }
}
