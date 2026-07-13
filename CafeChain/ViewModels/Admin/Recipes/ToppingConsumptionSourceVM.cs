using System.Collections.Generic;

namespace CafeChain.ViewModels.Admin.Recipes
{
    public static class ToppingConsumptionSourceCodes
    {
        public const string DirectIngredient = "DIRECT_INGREDIENT";
        public const string PreparedItem = "PREPARED_ITEM";
        public const string MixedOrInvalid = "MIXED_OR_INVALID";
        public const string NoActiveRecipe = "NO_ACTIVE_RECIPE";
    }

    /// <summary>
    /// Read-only trace from a topping to the exact active recipe and its pinned consumption sources.
    /// It never infers a PreparedItem relationship from topping/recipe names.
    /// </summary>
    public sealed class ToppingConsumptionSourceVM
    {
        public int ToppingId { get; set; }
        public int? ActiveRecipeId { get; set; }
        public string? ActiveRecipeCode { get; set; }
        public string SourceCode { get; set; } = ToppingConsumptionSourceCodes.NoActiveRecipe;
        public string SourceLabel { get; set; } = "Chưa cấu hình nguồn tiêu hao";
        public bool MappingValid { get; set; }
        public string Reason { get; set; } = "Topping chưa có công thức Active.";
        public decimal? EstimatedCostPerPortion { get; set; }
        public bool CostComplete { get; set; }
        public string CostStatus { get; set; } = "Chưa xác định giá vốn BOM";
        public List<ToppingConsumptionComponentVM> Components { get; set; } = new();
    }

    public sealed class ToppingConsumptionComponentVM
    {
        public string SourceKind { get; set; } = "";
        public int? IngredientId { get; set; }
        public string? IngredientCode { get; set; }
        public string? IngredientName { get; set; }
        public int? ChildRecipeId { get; set; }
        public string? ChildRecipeCode { get; set; }
        public string? ChildRecipeName { get; set; }
        public int? PreparedItemId { get; set; }
        public string? PreparedItemCode { get; set; }
        public string? PreparedItemName { get; set; }
        public string? PreparedItemBaseUnitCode { get; set; }
        public decimal Quantity { get; set; }
        public string UnitCode { get; set; } = "";

        public string IdentityDisplay => SourceKind == ToppingConsumptionSourceCodes.DirectIngredient
            ? $"[{IngredientCode ?? $"ING_{IngredientId}"}] {IngredientName ?? "Nguyên liệu không tồn tại"}"
            : $"[{PreparedItemCode ?? $"PI_{PreparedItemId}"}] {PreparedItemName ?? "BTP chưa mapping"}";

        public string QuantityDisplay => $"{Quantity:0.####} {UnitCode}".Trim();
    }
}
