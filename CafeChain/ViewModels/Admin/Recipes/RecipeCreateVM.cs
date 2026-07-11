using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System;

namespace CafeChain.ViewModels.Admin.Recipes
{
    public class RecipeCreateVM
    {
        // ===== LOẠI CÔNG THỨC =====
        // "POS" | "TOPPING" | "SUBRECIPE" — not persisted as DB RecipeType (#112)
        [Required(ErrorMessage = "Vui lòng chọn loại công thức")]
        public string RecipeType { get; set; } = "POS";

        // ===== CHO LOẠI POS (Món bán) =====
        public int? DrinkId { get; set; }
        public int? SizeId { get; set; }

        // ===== CHO LOẠI TOPPING =====
        public int? ToppingId { get; set; }

        // ===== CHO LOẠI SUBRECIPE / BTP (#112) =====
        /// <summary>Stable PreparedItem produced by this BTP recipe version.</summary>
        public int? PreparedItemId { get; set; }

        /// <summary>
        /// Free-text display only for legacy unmapped rows. New BTP identity is PreparedItemId.
        /// </summary>
        public string? SubRecipeName { get; set; }

        /// <summary>
        /// Expected net output after standard loss → maps to Recipe.OutputQuantity.
        /// Label UI: "Sản lượng dự kiến sau hao hụt chuẩn".
        /// </summary>
        public decimal? ExpectedYield { get; set; }

        public int? OutputUnitId { get; set; }

        /// <summary>Legacy unmapped BTP (no Drink/Topping/PreparedItem) — display/audit only.</summary>
        public bool IsLegacyUnmappedSubRecipe { get; set; }

        /// <summary>When true, PreparedItem selector is locked (existing BTP version chain).</summary>
        public bool PreparedItemLocked { get; set; }

        // ===== CHUNG =====
        public string? Description { get; set; }

        public decimal TotalCost { get; set; }

        public bool Active { get; set; } = true;

        [Required(ErrorMessage = "Ngày hiệu lực không được để trống")]
        public DateTime EffectiveDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Công thức phải có ít nhất một nguyên liệu")]
        public List<RecipeDetailVM> Details { get; set; } = new List<RecipeDetailVM>();
    }
}
