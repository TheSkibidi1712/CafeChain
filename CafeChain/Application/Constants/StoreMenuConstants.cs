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

    public static class POSCatalogSaleErrorCodes
    {
        public const string SnapshotRequired = "CATALOG_SNAPSHOT_REQUIRED";
        public const string SnapshotInvalid = "CATALOG_SNAPSHOT_INVALID";
        public const string SnapshotStale = "CATALOG_SNAPSHOT_STALE";
        public const string ItemUnavailable = "CATALOG_ITEM_UNAVAILABLE";
        public const string ToppingInvalid = "CATALOG_TOPPING_INVALID";

        public static bool IsConflict(string? errorCode) => errorCode is
            SnapshotRequired or SnapshotInvalid or SnapshotStale or ItemUnavailable or ToppingInvalid;
    }
}
