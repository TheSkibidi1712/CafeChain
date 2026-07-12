namespace CafeChain.Application.DTOs.Admin.Production
{
    public sealed class ProductionRunExecutionResultDto
    {
        public int ProductionRunId { get; set; }
        public int StoreId { get; set; }
        public int RecipeId { get; set; }
        public decimal RequestedRunCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool WasReplay { get; set; }
        public bool StockApplied { get; set; }
        public DateTime? CompletedAt { get; set; }
        public decimal? NormalizedOutputQuantity { get; set; }
        public int? OutputBaseUnitId { get; set; }
        public string? OutputBaseUnitCode { get; set; }
        public int? OutputStoreInventoryId { get; set; }
        public int? OutputPreparedItemId { get; set; }
        public string MessageKey { get; set; } = string.Empty;
        public List<ProductionRunMovementSummaryDto> Movements { get; set; } = new();
    }

    public sealed class ProductionRunMovementSummaryDto
    {
        public int InventoryTransactionId { get; set; }
        public int StoreInventoryId { get; set; }
        public string Type { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal BeforeQty { get; set; }
        public decimal AfterQty { get; set; }
    }

    public static class ProductionRunExecutionFailureCodes
    {
        public const string InvalidRequest = "INVALID_REQUEST";
        public const string RunNotFound = "PRODUCTION_RUN_NOT_FOUND";
        public const string InvalidStatus = "INVALID_PRODUCTION_RUN_STATUS";
        public const string StoreUnauthorized = "STORE_UNAUTHORIZED";
        public const string StaffUnauthorized = "STAFF_UNAUTHORIZED";
        public const string ModeLegacy = "LEGACY_WRITER_FORBIDDEN";
        public const string ModeBlocked = "BTP_WRITER_BLOCKED";
        public const string CapabilityNotReady = "PRODUCTION_WRITER_NOT_READY";
        public const string MissingWriterConfiguration = "MISSING_WRITER_CONFIGURATION";
        public const string RecipeNotFound = "RECIPE_NOT_FOUND";
        public const string InvalidOutputContract = "INVALID_OUTPUT_CONTRACT";
        public const string PreparedItemInvalid = "PREPARED_ITEM_INVALID";
        public const string ConversionFailed = "UNIT_CONVERSION_FAILED";
        public const string UnmappedChildRecipe = "UNMAPPED_CHILD_RECIPE";
        public const string MissingInputInventory = "MISSING_INPUT_INVENTORY";
        public const string InsufficientStock = "INSUFFICIENT_STOCK";
        public const string InventoryResolutionFailed = "INVENTORY_RESOLUTION_FAILED";
        public const string SelfConsumptionNotSupported = "SELF_CONSUMPTION_NOT_SUPPORTED";
        public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
        public const string ExecutionFailed = "EXECUTION_FAILED";
    }
}
