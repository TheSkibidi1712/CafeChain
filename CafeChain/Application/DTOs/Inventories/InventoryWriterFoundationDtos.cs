using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Stores;

namespace CafeChain.Application.DTOs.Inventories
{
    public sealed record InventoryWriterModeSnapshot(
        int StoreId,
        InventoryWriterMode WriterMode,
        byte[] ConfigurationRowVersion,
        Guid TransactionId);

    public sealed class InventoryWriterModeStatusDto
    {
        public int StoreId { get; init; }
        public InventoryWriterMode WriterMode { get; init; }
        public bool HasEverActivatedPreparedItem { get; init; }
        public byte[] RowVersion { get; init; } = Array.Empty<byte>();
        public DateTime UpdatedAt { get; init; }
    }

    public sealed record InventoryReadinessBlocker(string Code, string Message);

    public sealed class InventoryWriterReadinessReport
    {
        public int StoreId { get; init; }
        public bool Ready { get; init; }
        public string ReadinessHash { get; init; } = string.Empty;
        public IReadOnlyList<InventoryReadinessBlocker> Blockers { get; init; }
            = Array.Empty<InventoryReadinessBlocker>();
    }

    public sealed class InventoryWriterModeTransitionRequest
    {
        public int StoreId { get; init; }
        public InventoryWriterMode ExpectedCurrentMode { get; init; }
        public byte[] ExpectedRowVersion { get; init; } = Array.Empty<byte>();
        public InventoryWriterMode TargetMode { get; init; }
        public string? ReadinessHash { get; init; }
        public string Reason { get; init; } = string.Empty;
        public int ActorAccountId { get; init; }
    }

    public sealed class InventoryWriterModeTransitionResult
    {
        public bool Succeeded { get; init; }
        public string Message { get; init; } = string.Empty;
        public string? FailureCode { get; init; }
        public InventoryWriterModeStatusDto? Status { get; init; }
        public InventoryWriterReadinessReport? Readiness { get; init; }
    }

    public sealed record InventoryWriterCapabilityStatus(
        string CapabilityId,
        string ContractVersion,
        bool Ready,
        string? BlockerCode = null,
        string? BlockerMessage = null);

    public sealed class StoreInventoryWriteRequest
    {
        public InventoryWriterModeSnapshot ModeSnapshot { get; init; } = null!;
        public int StoreId { get; init; }
        public string IdentityType { get; init; } = InventoryWriteIdentityTypes.Ingredient;
        public int? IngredientId { get; init; }
        public int? RecipeId { get; init; }
        public int? PreparedItemId { get; init; }
        public int? NormalizedBaseUnitId { get; init; }
        public int? SourceRecipeId { get; init; }
        public bool AllowCreateIntent { get; init; }
    }

    public sealed class StoreInventoryWriteResolution
    {
        public string Status { get; init; } = InventoryWriteResolutionStatuses.NotFound;
        public string Message { get; init; } = string.Empty;
        public StoreInventory? StoreInventory { get; init; }
    }

    public static class InventoryWriterCapabilityIds
    {
        public const string ProductionPreparedWriter = "PRODUCTION_PREPARED_WRITER";
        public const string PosPreparedWriter = "POS_PREPARED_WRITER";
        public const string AlertRestockPreparedIdentity = "ALERT_RESTOCK_PREPARED_IDENTITY";
        public const string ConsolidationOrNoopEvidence = "CONSOLIDATION_OR_NOOP_EVIDENCE";

        public static readonly IReadOnlyList<string> Required = new[]
        {
            ProductionPreparedWriter,
            PosPreparedWriter,
            AlertRestockPreparedIdentity,
            ConsolidationOrNoopEvidence
        };
    }

    public static class InventoryWriteIdentityTypes
    {
        public const string Ingredient = "INGREDIENT";
        public const string LegacyRecipe = "LEGACY_RECIPE";
        public const string PreparedItem = "PREPARED_ITEM";
    }

    public static class InventoryWriteResolutionStatuses
    {
        public const string FoundCanonical = "FOUND_CANONICAL";
        public const string CreateAllowed = "CREATE_ALLOWED";
        public const string BlockedMode = "BLOCKED_MODE";
        public const string UnknownQuantitySemantics = "UNKNOWN_QUANTITY_SEMANTICS";
        public const string Collision = "COLLISION";
        public const string Superseded = "SUPERSEDED";
        public const string UnitMismatch = "UNIT_MISMATCH";
        public const string InvalidMapping = "INVALID_MAPPING";
        public const string NotFound = "NOT_FOUND";
        public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    }

    public static class InventoryWriterFailureCodes
    {
        public const string MissingConfiguration = "MISSING_WRITER_CONFIGURATION";
        public const string MissingTransaction = "MISSING_INVENTORY_TRANSACTION";
        public const string InvalidSnapshot = "INVALID_MODE_SNAPSHOT";
        public const string ModeBlocked = "BTP_WRITER_BLOCKED";
        public const string LegacyWriterForbidden = "LEGACY_WRITER_FORBIDDEN";
        public const string Unauthorized = "UNAUTHORIZED_MODE_TRANSITION";
        public const string InvalidTransition = "INVALID_MODE_TRANSITION";
        public const string StaleConfiguration = "STALE_WRITER_CONFIGURATION";
        public const string ReadinessFailed = "PREPARED_WRITER_NOT_READY";
    }
}
