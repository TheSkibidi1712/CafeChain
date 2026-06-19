using System.ComponentModel.DataAnnotations.Schema;

namespace CafeChain.Models.Drinks
{
    public class Recipe
    {
        public int RecipeId { get; set; }

        public string Name { get; set; }

        public decimal YieldPercentage { get; set; } = 100;
        // hao hụt: 95% nghĩa là mất 5%

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
        public int? ToppingId { get; set; }

        public virtual ICollection<RecipeDetail> RecipeDetails { get; set; }

        public virtual ICollection<RecipeDetail> ChildRecipeDetails { get; set; }     // dùng ChildRecipeId

    }
}
