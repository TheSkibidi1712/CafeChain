namespace CafeChain.Application.Constants
{
    /// <summary>Issue #97 — StockAlert type codes.</summary>
    public static class StockAlertTypes
    {
        public const string LowStock = "LOW_STOCK";
        public const string OutOfStock = "OUT_OF_STOCK";
    }

    /// <summary>Issue #97 — StockAlert status codes.</summary>
    public static class StockAlertStatuses
    {
        public const string Open = "OPEN";
        public const string Resolved = "RESOLVED";
        // Future: CONFIRMED, MANAGER_REJECTED
    }

    /// <summary>Issue #97 — StockAlert severity codes.</summary>
    public static class StockAlertSeverities
    {
        public const string Warning = "WARNING";
        public const string Urgent = "URGENT";
    }

    /// <summary>Issue #97 — StockAlert evaluation sources.</summary>
    public static class StockAlertSources
    {
        public const string Auto = "AUTO";
        public const string ManualCheck = "MANUAL_CHECK";
        public const string PosSale = "POS_SALE";
        public const string OfflineSync = "OFFLINE_SYNC";
        public const string InventoryTransaction = "INVENTORY_TRANSACTION";
    }
}
