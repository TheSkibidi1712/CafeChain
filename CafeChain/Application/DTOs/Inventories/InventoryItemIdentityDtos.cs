namespace CafeChain.Application.DTOs.Inventories
{
    /// <summary>Stable read identity for a StoreInventory row during #115 compatibility.</summary>
    public sealed class InventoryItemIdentitySnapshot
    {
        public int StoreInventoryId { get; init; }
        public int StoreId { get; init; }
        public string InventoryItemType { get; init; } = InventoryItemIdentityTypes.Unknown;
        public int? IngredientId { get; init; }
        public int? PreparedItemId { get; init; }
        public int? LegacyRecipeId { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public int? BaseUnitId { get; init; }
        public string? BaseUnitCode { get; init; }
        public bool IsLegacyUnmapped { get; init; }
        public bool HasCompatibilityRecipe { get; init; }
        public string QuantitySemanticsStatus { get; init; } = QuantitySemanticsStatuses.Unknown;
        public IReadOnlyList<string> ValidationIssues { get; init; } = Array.Empty<string>();
    }

    public sealed class PreparedItemInventoryCompatibilityReport
    {
        public int StoreInventoryId { get; init; }
        public int StoreId { get; init; }
        public int? RecipeId { get; init; }
        public int ProposedPreparedItemId { get; init; }
        public decimal? RecipeOutputQuantity { get; init; }
        public int? RecipeOutputUnitId { get; init; }
        public int? PreparedItemBaseUnitId { get; init; }
        public string? PreparedItemBaseUnitCode { get; init; }
        public decimal AvailableQty { get; init; }
        public decimal ReservedQty { get; init; }
        public decimal? MinStockLevel { get; init; }
        public decimal? MaxNegativeQty { get; init; }
        public bool RecipePreparedItemConsistent { get; init; }
        public bool UnitsPhysicallyCompatible { get; init; }
        public string QuantitySemanticsStatus { get; init; } = QuantitySemanticsStatuses.Unknown;
        public string CollisionStatus { get; init; } = CompatibilityCollisionStatuses.None;
        public IReadOnlyList<int> InvolvedStoreInventoryIds { get; init; } = Array.Empty<int>();
        public decimal BeforeAvailableTotal { get; init; }
        public decimal BeforeReservedTotal { get; init; }
        public decimal HypotheticalAfterAvailableTotal { get; init; }
        public decimal HypotheticalAfterReservedTotal { get; init; }
        public bool NumericConservationConfirmed { get; init; }
        public string ProposedAction { get; init; } = CompatibilityProposedActions.Blocked;
        public IReadOnlyList<string> BlockingIssues { get; init; } = Array.Empty<string>();
    }

    public static class InventoryItemIdentityTypes
    {
        public const string Ingredient = "INGREDIENT";
        public const string PreparedItem = "PREPARED_ITEM";
        public const string LegacyRecipe = "LEGACY_RECIPE";
        public const string Unknown = "UNKNOWN";
    }

    public static class QuantitySemanticsStatuses
    {
        public const string NotApplicable = "NOT_APPLICABLE";
        public const string NoBtpRow = "NO_BTP_ROW";
        public const string BaseUnitQuantityConfirmed = "BASE_UNIT_QUANTITY_CONFIRMED";
        public const string LegacyBatchQuantity = "LEGACY_BATCH_QUANTITY";
        public const string Unknown = "QUANTITY_SEMANTICS_UNKNOWN";
        public const string UnitIncompatible = "UNIT_INCOMPATIBLE";
        public const string Collision = "COLLISION";
    }

    public static class CompatibilityCollisionStatuses
    {
        public const string None = "NONE";
        public const string Collision = "COLLISION";
    }

    public static class CompatibilityProposedActions
    {
        public const string ReadyForMetadataMapping = "READY_FOR_METADATA_MAPPING";
        public const string Blocked = "BLOCKED";
    }

    public static class InventoryIdentityValidationIssueCodes
    {
        public const string NoIdentity = "NO_IDENTITY";
        public const string InvalidIngredientCombination = "INVALID_INGREDIENT_COMBINATION";
        public const string MissingPreparedItem = "MISSING_PREPARED_ITEM";
        public const string InactivePreparedItem = "INACTIVE_PREPARED_ITEM";
        public const string RecipePreparedItemMismatch = "RECIPE_PREPARED_ITEM_MISMATCH";
        public const string MissingRecipe = "MISSING_RECIPE";
        public const string MissingRecipeOutputUnit = "MISSING_RECIPE_OUTPUT_UNIT";
        public const string UnitIncompatible = "UNIT_INCOMPATIBLE";
        public const string ExistingTargetRow = "EXISTING_TARGET_ROW";
        public const string MultipleLegacyRows = "MULTIPLE_LEGACY_ROWS";
        public const string MinStockLevelConflict = "MIN_STOCK_LEVEL_CONFLICT";
        public const string MaxNegativeQtyConflict = "MAX_NEGATIVE_QTY_CONFLICT";
        public const string QuantitySemanticsUnknown = "QUANTITY_SEMANTICS_UNKNOWN";
    }
}
