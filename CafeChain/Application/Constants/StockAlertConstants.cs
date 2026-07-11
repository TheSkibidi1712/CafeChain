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
        /// <summary>Issue #99 — StoreManager confirmed alert.</summary>
        public const string Confirmed = "CONFIRMED";
        /// <summary>Issue #99 — StoreManager rejected (false positive).</summary>
        public const string ManagerRejected = "MANAGER_REJECTED";
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
        /// <summary>Issue #98 — manual shortage report from POS.</summary>
        public const string SalesReport = "SALES_REPORT";
    }

    /// <summary>Issue #98 — StaffNotification type codes.</summary>
    public static class StaffNotificationTypes
    {
        public const string StockShortageReport = "STOCK_SHORTAGE_REPORT";
        /// <summary>Issue #99 — notify reporter that manager confirmed.</summary>
        public const string StockAlertConfirmed = "STOCK_ALERT_CONFIRMED";
        /// <summary>Issue #99 — notify reporter that manager rejected.</summary>
        public const string StockAlertRejected = "STOCK_ALERT_REJECTED";
        /// <summary>Issue #100 — notify AccountantWarehouse of new restock request.</summary>
        public const string RestockRequestSubmitted = "RESTOCK_REQUEST_SUBMITTED";
    }

    /// <summary>Issue #98 — StaffNotification entity type codes.</summary>
    public static class StaffNotificationEntityTypes
    {
        public const string StockAlert = "StockAlert";
        /// <summary>Issue #100 — RestockRequest entity deep-link.</summary>
        public const string RestockRequest = "RestockRequest";
    }
}
