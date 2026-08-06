using System.Text.Json;
using System.Text.Json.Serialization;

namespace CafeChain.Application.DTOs.Admin.Profitability
{
    public sealed class DrinkProfitabilityPreviewDto
    {
        public int StoreId { get; init; }
        public string StoreName { get; init; } = string.Empty;
        public int DrinkId { get; init; }
        public string DrinkName { get; init; } = string.Empty;
        public DateTime CostTimestampUtc { get; init; }
        public string CostSource { get; init; } = "FIFO_CURRENT_STORE";
        public bool SellingPriceIsGlobal { get; init; } = true;
        public IReadOnlyList<DrinkSizeProfitabilityRowDto> Sizes { get; init; } = Array.Empty<DrinkSizeProfitabilityRowDto>();
    }

    public sealed class DrinkSizeProfitabilityRowDto
    {
        public int DrinkSizeId { get; init; }
        public int SizeId { get; init; }
        public string SizeName { get; init; } = string.Empty;
        public int? RecipeId { get; init; }
        public string? RecipeCode { get; init; }
        public DateTime? RecipeEffectiveDate { get; init; }
        public string RecipeStatus { get; init; } = string.Empty;
        public string CostStatus { get; init; } = string.Empty;
        public string CostMessage { get; init; } = string.Empty;
        public decimal KnownCost { get; init; }
        public decimal? EstimatedCost { get; init; }
        public decimal? BomConfigurationCost { get; init; }
        public string BomConfigurationCostStatus { get; init; } = string.Empty;
        public decimal CurrentGlobalPrice { get; init; }
        public decimal DefaultToppingPriceImpact { get; init; }
        public decimal EffectiveSellingPrice { get; init; }
        public decimal? GrossProfit { get; init; }
        public decimal? GrossMarginPercent { get; init; }
        public decimal? MarkupPercent { get; init; }
        public string RowVersion { get; init; } = string.Empty;
        public IReadOnlyList<FifoCostComponentDto> Components { get; init; } = Array.Empty<FifoCostComponentDto>();
        public IReadOnlyList<ProfitabilityToppingPolicyDto> DefaultToppings { get; init; } = Array.Empty<ProfitabilityToppingPolicyDto>();
        public IReadOnlyList<CostSectionCompletenessDto> CostSections { get; init; } = Array.Empty<CostSectionCompletenessDto>();
    }

    public sealed class CostSectionCompletenessDto
    {
        public string Section { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }

    public sealed class FifoCostComponentDto
    {
        public string Source { get; init; } = string.Empty;
        public string ItemType { get; init; } = string.Empty;
        public int ItemId { get; init; }
        public string ItemName { get; init; } = string.Empty;
        public decimal RequiredQuantity { get; init; }
        public decimal AvailableCostQuantity { get; init; }
        public decimal CoveredQuantity { get; init; }
        public decimal MissingQuantity { get; init; }
        public decimal KnownCost { get; init; }
        public string UnitName { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public string SourceLabel { get; init; } = string.Empty;
        public string ItemTypeLabel { get; init; } = string.Empty;
    }

    public sealed class ProfitabilityToppingPolicyDto
    {
        public int ToppingId { get; init; }
        public string ToppingName { get; init; } = string.Empty;
        public decimal QuantityPerDrink { get; init; }
        public string PriceTreatment { get; init; } = string.Empty;
        public string CostTreatment { get; init; } = string.Empty;
        public decimal PriceImpact { get; init; }
        public decimal? CostImpact { get; init; }
        public string CostStatus { get; init; } = string.Empty;
    }

    public sealed class PriceSuggestionRequest
    {
        public decimal EstimatedCost { get; set; }
        public decimal CurrentSellingPrice { get; set; }
        public string TargetMode { get; set; } = string.Empty;
        public decimal TargetValue { get; set; }
        public string RoundingMode { get; set; } = Constants.ProfitabilityRoundingModes.None;
    }

    public sealed class PriceSuggestionResult
    {
        public bool IsValid { get; init; }
        public string Message { get; init; } = string.Empty;
        public decimal GrossProfit { get; init; }
        public decimal? GrossMarginPercent { get; init; }
        public decimal? MarkupPercent { get; init; }
        public decimal RawSuggestedPrice { get; init; }
        public decimal RoundedSuggestedPrice { get; init; }
        public decimal EffectiveGrossProfit { get; init; }
        public decimal? EffectiveMarginPercent { get; init; }
        public decimal? EffectiveMarkupPercent { get; init; }
    }

    public sealed class UpdateDrinkSizePriceRequest
    {
        public int DrinkSizeId { get; set; }
        public decimal NewSellingPrice { get; set; }
        public string ExpectedRowVersion { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public bool ConfirmIncompleteCost { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? UnexpectedFields { get; set; }
    }

    public sealed class DrinkSizePriceUpdateResult
    {
        public int DrinkSizeId { get; init; }
        public decimal OldPrice { get; init; }
        public decimal NewPrice { get; init; }
        public string RowVersion { get; init; } = string.Empty;
        public long CatalogVersion { get; init; }
    }

    public sealed class PosCatalogVersionDto
    {
        public int StoreId { get; init; }
        public long Version { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }
}
