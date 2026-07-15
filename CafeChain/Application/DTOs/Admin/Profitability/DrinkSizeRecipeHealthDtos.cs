using CafeChain.Models.Drinks;

namespace CafeChain.Application.DTOs.Admin.Profitability
{
    public sealed class DrinkSizeRecipeResolution
    {
        public string Status { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public Recipe? Recipe { get; init; }
        public int CandidateCount { get; init; }
        public bool HasGenericFallback { get; init; }
        public bool IsReady => Status == Constants.DrinkSizeRecipeHealthStatuses.ExactReady && Recipe != null;
    }

    public sealed class DrinkSizeRecipeHealthRow
    {
        public int DrinkSizeId { get; init; }
        public int DrinkId { get; init; }
        public string DrinkName { get; init; } = string.Empty;
        public int SizeId { get; init; }
        public string SizeName { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public int? RecipeId { get; init; }
        public string? RecipeCode { get; init; }
        public DateTime? EffectiveDate { get; init; }
        public bool HasGenericFallback { get; init; }
    }
}
