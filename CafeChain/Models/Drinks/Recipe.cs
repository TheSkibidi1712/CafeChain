using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;

namespace CafeChain.Models.Drinks
{
    public class Recipe
    {
        public int RecipeId { get; set; }

        public string RecipeCode { get; set; }

        public string Name { get; set; }

        public decimal YieldPercentage { get; set; } = 100;
        // hao hụt: 95% nghĩa là mất 5% — legacy; BTP net output is OutputQuantity (ADR-0006 / #112)

        public bool Active { get; set; }

        // === VERSIONING SYSTEM ===
        /// <summary>
        /// Trạng thái vòng đời: Active | Archived
        /// </summary>
        public string Status { get; set; } = "Active";

        /// <summary>
        /// Ngày hiệu lực của phiên bản công thức này
        /// </summary>
        public DateTime? EffectiveDate { get; set; }

        /// <summary>
        /// Trỏ về phiên bản trước đó (Audit Trail cho Versioning)
        /// </summary>
        public int? ParentVersionId { get; set; }
        public virtual Recipe ParentVersion { get; set; }
        public virtual ICollection<Recipe> ChildVersions { get; set; }

        // Relationships for inventory lookup
        public int? DrinkId { get; set; }
        public int? SizeId { get; set; }
        public int? ToppingId { get; set; }

        /// <summary>
        /// Stable BTP product this version produces (ADR-0006 / Issue #112).
        /// Null for POS drink, topping, and legacy unmapped SUBRECIPE rows.
        /// </summary>
        public int? PreparedItemId { get; set; }

        /// <summary>
        /// Expected net output for one standard production run (after normal loss).
        /// Authoritative for BTP; do not re-apply YieldPercentage (#117 for cost).
        /// </summary>
        public decimal? OutputQuantity { get; set; }

        /// <summary>
        /// Unit of OutputQuantity; convertible to PreparedItem.BaseUnitId via physical conversion.
        /// </summary>
        public int? OutputUnitId { get; set; }

        /// <summary>
        /// Optional yield-variance tolerance override for production acceptance.
        /// Null uses the system production default.
        /// </summary>
        public decimal? YieldVarianceTolerancePercent { get; set; }

        public virtual Size Size { get; set; }

        public virtual PreparedItem? PreparedItem { get; set; }

        public virtual Unit? OutputUnit { get; set; }

        public virtual ICollection<RecipeDetail> RecipeDetails { get; set; }

        public virtual ICollection<RecipeDetail> ChildRecipeDetails { get; set; }     // dùng ChildRecipeId

    }
}
