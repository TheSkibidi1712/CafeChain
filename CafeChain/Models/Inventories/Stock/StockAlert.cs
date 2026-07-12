using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories.Stock
{
    /// <summary>
    /// Issue #97 — persisted LOW_STOCK / OUT_OF_STOCK alert for a store inventory item.
    /// Issue #122 — transitional identity: Ingredient-only, Recipe-only, Recipe+PreparedItem, PreparedItem-only.
    /// Issue #98 — optional sales-report fields (ReportedBy / ReportedAt / SALES_REPORT source).
    /// </summary>
    public class StockAlert
    {
        public int StockAlertId { get; set; }

        public int StoreId { get; set; }

        public int? IngredientId { get; set; }

        public int? RecipeId { get; set; }

        /// <summary>Issue #122 — stable BTP identity for PreparedItem-mode alerts.</summary>
        public int? PreparedItemId { get; set; }

        /// <summary>LOW_STOCK | OUT_OF_STOCK</summary>
        public string AlertType { get; set; } = string.Empty;

        /// <summary>WARNING | URGENT</summary>
        public string Severity { get; set; } = string.Empty;

        /// <summary>OPEN | RESOLVED | CONFIRMED | MANAGER_REJECTED</summary>
        public string Status { get; set; } = string.Empty;

        public decimal CurrentQtySnapshot { get; set; }

        public decimal? ThresholdSnapshot { get; set; }

        /// <summary>AUTO | MANUAL_CHECK | POS_SALE | OFFLINE_SYNC | INVENTORY_TRANSACTION | SALES_REPORT</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>Latest report note (Issue #98 — overwrite, no history). Do not use for manager decision.</summary>
        public string? Note { get; set; }

        /// <summary>Issue #98 — latest staff who reported shortage (null for auto detection).</summary>
        public int? ReportedByStaffId { get; set; }

        /// <summary>Issue #98 — latest shortage report time (UTC).</summary>
        public DateTime? ReportedAt { get; set; }

        /// <summary>Issue #99 — StoreManager who confirmed.</summary>
        public int? ConfirmedByStaffId { get; set; }

        public DateTime? ConfirmedAt { get; set; }

        /// <summary>Issue #99 — mandatory manager note on confirm.</summary>
        public string? ManagerNote { get; set; }

        /// <summary>Issue #99 — StoreManager who rejected.</summary>
        public int? RejectedByStaffId { get; set; }

        public DateTime? RejectedAt { get; set; }

        /// <summary>Issue #99 — mandatory reject reason.</summary>
        public string? RejectReason { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public DateTime? ResolvedAt { get; set; }

        public string? ResolvedReason { get; set; }

        public virtual Store Store { get; set; } = null!;
        public virtual Ingredient? Ingredient { get; set; }
        public virtual Recipe? Recipe { get; set; }
        public virtual PreparedItem? PreparedItem { get; set; }
        public virtual Staff? ReportedByStaff { get; set; }
        public virtual Staff? ConfirmedByStaff { get; set; }
        public virtual Staff? RejectedByStaff { get; set; }
    }
}
