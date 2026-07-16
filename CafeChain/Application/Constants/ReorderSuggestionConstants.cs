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
        public const string Unknown = "UNKNOWN";
    }
}
