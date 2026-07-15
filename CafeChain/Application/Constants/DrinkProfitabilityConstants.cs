namespace CafeChain.Application.Constants
{
    public static class DrinkSizeRecipeHealthStatuses
    {
        public const string ExactReady = "EXACT_READY";
        public const string GenericFallbackOnly = "GENERIC_FALLBACK_ONLY";
        public const string MissingRecipe = "MISSING_RECIPE";
        public const string MultipleActiveRecipe = "MULTIPLE_ACTIVE_RECIPE";
        public const string InvalidRecipe = "INVALID_RECIPE";
        public const string FutureRecipeOnly = "FUTURE_RECIPE_ONLY";
    }

    public static class ProfitabilityCostStatuses
    {
        public const string Complete = "COMPLETE";
        public const string MissingRecipe = "MISSING_RECIPE";
        public const string MultipleActiveRecipe = "MULTIPLE_ACTIVE_RECIPE";
        public const string MissingConversion = "MISSING_CONVERSION";
        public const string MissingCostLayer = "MISSING_COST_LAYER";
        public const string InsufficientCostQuantity = "INSUFFICIENT_COST_QUANTITY";
        public const string InvalidBom = "INVALID_BOM";
        public const string InactiveRecipe = "INACTIVE_RECIPE";
        public const string MissingDefaultToppingPolicy = "MISSING_DEFAULT_TOPPING_POLICY";
        public const string Incomplete = "INCOMPLETE";
    }

    public static class ToppingPriceTreatments
    {
        public const string IncludedInBasePrice = "INCLUDED_IN_BASE_PRICE";
        public const string AddToppingPrice = "ADD_TOPPING_PRICE";
        public static readonly string[] All = { IncludedInBasePrice, AddToppingPrice };
    }

    public static class ToppingCostTreatments
    {
        public const string IncludedInDrinkRecipe = "INCLUDED_IN_DRINK_RECIPE";
        public const string AddToppingRecipeCost = "ADD_TOPPING_RECIPE_COST";
        public const string DisplayOnly = "DISPLAY_ONLY";
        public static readonly string[] All = { IncludedInDrinkRecipe, AddToppingRecipeCost, DisplayOnly };
    }

    public static class ProfitabilityTargetModes
    {
        public const string Margin = "MARGIN";
        public const string Markup = "MARKUP";
        public const string ProfitAmount = "PROFIT_AMOUNT";
    }

    public static class ProfitabilityRoundingModes
    {
        public const string None = "NONE";
        public const string Nearest500 = "NEAREST_500";
        public const string Ceiling500 = "CEILING_500";
        public const string Nearest1000 = "NEAREST_1000";
        public const string Ceiling1000 = "CEILING_1000";
        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
        {
            None, Nearest500, Ceiling500, Nearest1000, Ceiling1000
        };
    }
}
