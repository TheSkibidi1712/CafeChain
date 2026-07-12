using System.Collections.Generic;

namespace CafeChain.ViewModels.Admin.Recipes
{
    public enum RecipeBomTreeNodeKind
    {
        Ingredient,
        ChildRecipe,
        DepthLimit,
        CycleDetected
    }

    /// <summary>#129 Typed BOM tree node — pin ChildRecipeId, no latest-active substitution.</summary>
    public class RecipeBomTreeNodeVM
    {
        public RecipeBomTreeNodeKind Kind { get; set; }
        public int? IngredientId { get; set; }
        public int? ChildRecipeId { get; set; }
        public string DisplayName { get; set; } = "";
        public decimal Quantity { get; set; }
        public string UnitName { get; set; } = "";
        public int Depth { get; set; }
        public string? Message { get; set; }
        public List<RecipeBomTreeNodeVM> Children { get; set; } = new();
    }

    public class RecipeBomTreeResult
    {
        public int RootRecipeId { get; set; }
        public string RootName { get; set; } = "";
        public int MaxDepth { get; set; } = 5;
        public List<RecipeBomTreeNodeVM> Roots { get; set; } = new();
        public bool RootNotFound { get; set; }
    }
}
