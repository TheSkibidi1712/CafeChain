using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories.Stock
{
    /// <summary>
    /// Issue #100 — official restock request from a CONFIRMED StockAlert.
    /// Does not mutate inventory or create InventoryDocument.
    /// Issue #122 — transitional PreparedItem identity copied from StockAlert.
    /// </summary>
    public class RestockRequest
    {
        public int RestockRequestId { get; set; }

        public int StockAlertId { get; set; }

        public int StoreId { get; set; }

        public int? IngredientId { get; set; }

        public int? RecipeId { get; set; }

        /// <summary>Issue #122 — stable BTP identity when alert is PreparedItem-based.</summary>
        public int? PreparedItemId { get; set; }

        public decimal RequestedQuantity { get; set; }

        public decimal? SuggestedQuantity { get; set; }

        /// <summary>SUBMITTED | PROCESSING | COMPLETED | REJECTED | CANCELLED</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>NORMAL | HIGH | URGENT</summary>
        public string Priority { get; set; } = string.Empty;

        public int CreatedByStaffId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string? Note { get; set; }

        /// <summary>Reserved for future warehouse processing.</summary>
        public int? HandledByStaffId { get; set; }

        /// <summary>Reserved for future warehouse processing.</summary>
        public DateTime? HandledAt { get; set; }

        public virtual StockAlert StockAlert { get; set; } = null!;
        public virtual Store Store { get; set; } = null!;
        public virtual Ingredient? Ingredient { get; set; }
        public virtual Recipe? Recipe { get; set; }
        public virtual PreparedItem? PreparedItem { get; set; }
        public virtual Staff CreatedByStaff { get; set; } = null!;
        public virtual Staff? HandledByStaff { get; set; }
    }
}
