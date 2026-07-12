namespace CafeChain.Application.Constants
{
    /// <summary>Issue #119 / #131 structured error codes (status lives on ProductionRunStatus enum).</summary>
    public static class ProductionRunFailureCodes
    {
        public const string InvalidRequest = "INVALID_REQUEST";
        public const string InvalidRequestKey = "INVALID_REQUEST_KEY";
        public const string InvalidRunCount = "INVALID_RUN_COUNT";
        public const string RecipeNotFound = "RECIPE_NOT_FOUND";
        public const string InvalidRecipeOutput = "INVALID_RECIPE_OUTPUT";
        public const string StoreNotFound = "STORE_NOT_FOUND";
        public const string StoreUnauthorized = "STORE_UNAUTHORIZED";
        public const string StaffUnauthorized = "STAFF_UNAUTHORIZED";
        public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
        public const string MissingWriterConfiguration = "MISSING_WRITER_CONFIGURATION";
        public const string ModeBlocked = "BTP_WRITER_BLOCKED";
        /// <summary>Issue #131 — new production intent rejected for LegacyRecipe stores (graduation).</summary>
        public const string ModeLegacy = "BTP_WRITER_LEGACY";
        /// <summary>PRODUCTION_PREPARED_WRITER capability not registered or not Ready.</summary>
        public const string CapabilityNotReady = "PRODUCTION_CAPABILITY_NOT_READY";
        /// <summary>Obsolete alias — prefer CapabilityNotReady (#131).</summary>
        public const string ProductionWriterNotReady = CapabilityNotReady;
    }
}
