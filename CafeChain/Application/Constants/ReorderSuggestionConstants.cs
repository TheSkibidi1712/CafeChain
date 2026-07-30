namespace CafeChain.Application.Constants
{
    public static class ReorderSuggestionStatuses
    {
        // Legacy operational statuses are kept for API compatibility.  New
        // callers must use ReorderRecommendationLevels (the canonical
        // business status) instead of inferring readiness from this field.
        public const string Ready = "READY";
        public const string NoReorderNeeded = "NO_REORDER_NEEDED";
        public const string InsufficientHistory = "INSUFFICIENT_HISTORY";
        public const string MissingThreshold = "MISSING_THRESHOLD";
        public const string NoActiveSupplier = "NO_ACTIVE_SUPPLIER";
        public const string InvalidConversion = "INVALID_CONVERSION";
        public const string MissingLeadTime = "MISSING_LEAD_TIME";
        public const string MissingCost = "MISSING_COST";
        public const string IncomingCoversDemand = "INCOMING_COVERS_DEMAND";
        public const string ProcurementInProgress = "PROCUREMENT_IN_PROGRESS";
        public const string Unknown = "UNKNOWN";

        public const string CalculationVersion = "REORDER_RULES_V3";

        public static readonly IReadOnlySet<string> CanonicalValues =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ReorderRecommendationLevels.Urgent,
                ReorderRecommendationLevels.NearReorder,
                ReorderRecommendationLevels.Normal,
                ReorderRecommendationLevels.IncomingCoversDemand,
                ReorderRecommendationLevels.ProcurementInProgress,
                ReorderRecommendationLevels.DataIncomplete
            };
    }

    public static class ReorderRecommendationLevels
    {
        public const string Urgent = "URGENT";
        public const string NearReorder = "NEAR_REORDER";
        public const string Normal = "NORMAL";
        public const string ProcurementInProgress = "PROCUREMENT_IN_PROGRESS";
        public const string IncomingCoversDemand = "INCOMING_COVERS_DEMAND";
        public const string DataIncomplete = "DATA_INCOMPLETE";
    }

    /// <summary>
    /// Stable machine-readable reasons.  UI/AI may localise the accompanying
    /// message, but deduplication and tests use these codes.
    /// </summary>
    public static class ReorderSuggestionReasonCodes
    {
        public const string MissingThreshold = "MISSING_THRESHOLD";
        public const string NegativeThreshold = "NEGATIVE_THRESHOLD";
        public const string InsufficientHistory = "INSUFFICIENT_HISTORY";
        public const string NoActiveSupplier = "NO_ACTIVE_SUPPLIER";
        public const string MultiplePrimarySuppliers = "MULTIPLE_PRIMARY_SUPPLIERS";
        public const string InvalidConversion = "INVALID_CONVERSION";
        public const string MissingLeadTime = "MISSING_LEAD_TIME";
        public const string NegativeLeadTime = "NEGATIVE_LEAD_TIME";
        public const string MissingCost = "MISSING_COST";
        public const string InvalidCost = "INVALID_COST";
        public const string InvalidPackage = "INVALID_PACKAGE";
        public const string InvalidMoq = "INVALID_MOQ";
        public const string IncomingCoversDemand = "INCOMING_COVERS_DEMAND";
        public const string ProcurementInProgress = "PROCUREMENT_IN_PROGRESS";
        public const string NoReorderNeeded = "NO_REORDER_NEEDED";
        public const string RawDemand = "RAW_DEMAND";
        public const string RemainingDemand = "REMAINING_DEMAND";
        public const string CalculationOverflow = "CALCULATION_OVERFLOW";
        public const string InvalidInventory = "INVALID_INVENTORY";
        public const string PriceHistoryMissing = "PRICE_HISTORY_MISSING";
        public const string PriceHistoryInvalid = "PRICE_HISTORY_INVALID";
    }
}
