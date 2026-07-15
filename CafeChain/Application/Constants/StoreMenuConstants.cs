namespace CafeChain.Application.Constants
{
    public static class StoreMenuAvailabilityStatuses
    {
        public const string Available = "AVAILABLE";
        public const string LowStock = "LOW_STOCK";
        public const string OutOfStock = "OUT_OF_STOCK";
        public const string RecipeInvalid = "RECIPE_INVALID";
        public const string ToppingUnavailable = "TOPPING_UNAVAILABLE";
        public const string StoreNotReady = "STORE_NOT_READY";
        public const string Unknown = "UNKNOWN";

        public static bool IsSellable(string status) =>
            status is Available or LowStock;
    }
}
