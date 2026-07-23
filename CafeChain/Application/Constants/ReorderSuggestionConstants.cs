namespace CafeChain.Application.Constants
{
    public static class ReorderSuggestionStatuses
    {
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
}
