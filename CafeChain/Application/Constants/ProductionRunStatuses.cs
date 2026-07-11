namespace CafeChain.Application.Constants
{
    /// <summary>Issue #119 structured error codes (status lives on ProductionRunStatus enum).</summary>
    public static class ProductionRunFailureCodes
    {
        public const string InvalidRequest = "INVALID_REQUEST";
        public const string InvalidRequestKey = "INVALID_REQUEST_KEY";
        public const string InvalidRunCount = "INVALID_RUN_COUNT";
        public const string RecipeNotFound = "RECIPE_NOT_FOUND";
        public const string StoreNotFound = "STORE_NOT_FOUND";
        public const string StoreUnauthorized = "STORE_UNAUTHORIZED";
        public const string StaffUnauthorized = "STAFF_UNAUTHORIZED";
        public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
        public const string MissingWriterConfiguration = "MISSING_WRITER_CONFIGURATION";
        public const string ModeBlocked = "BTP_WRITER_BLOCKED";
        public const string ProductionWriterNotReady = "PRODUCTION_WRITER_NOT_READY";
    }
}
