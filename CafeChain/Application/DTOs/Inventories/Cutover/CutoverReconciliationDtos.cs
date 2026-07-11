using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Application.DTOs.Inventories.Cutover
{
    public static class CutoverContractVersions
    {
        public const string Reconciliation = "124.1";
        public const string Schema = "124.1";
    }

    public static class CutoverFailureCodes
    {
        public const string MaintenanceWindowRequired = "MAINTENANCE_WINDOW_REQUIRED";
        public const string SchemaContractNotReady = "SCHEMA_CONTRACT_NOT_READY";
        public const string SchemaProbeFailed = "SCHEMA_PROBE_FAILED";
        public const string ReconciliationNotClean = "RECONCILIATION_NOT_CLEAN";
        public const string StaleReconciliationHash = "STALE_RECONCILIATION_HASH";
        public const string StaleReadinessHash = "STALE_READINESS_HASH";
        public const string StaleSchemaHash = "STALE_SCHEMA_HASH";
        public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
        public const string LegacyBtpWritesGloballyDisabled = "LEGACY_BTP_WRITES_GLOBALLY_DISABLED";
        public const string Unauthorized = "UNAUTHORIZED_CUTOVER";
        public const string InvalidRequest = "INVALID_CUTOVER_REQUEST";
        public const string StoreNotFound = "STORE_NOT_FOUND";
        public const string GraduationNotComplete = "GRADUATION_NOT_COMPLETE";
    }

    public static class CutoverAnomalyCodes
    {
        public const string RecipeOnlyBtpRow = "RECIPE_ONLY_BTP_ROW";
        public const string PreparedItemMappingMissing = "PREPARED_ITEM_MAPPING_MISSING";
        public const string PreparedItemInactive = "PREPARED_ITEM_INACTIVE";
        public const string PreparedItemBaseUnitMissing = "PREPARED_ITEM_BASE_UNIT_MISSING";
        public const string UnknownQuantitySemantics = "UNKNOWN_QUANTITY_SEMANTICS";
        public const string UnitMismatch = "UNIT_MISMATCH";
        public const string MultipleCanonicalRows = "MULTIPLE_CANONICAL_ROWS";
        public const string CanonicalAndActiveLegacyCollision = "CANONICAL_AND_ACTIVE_LEGACY_COLLISION";
        public const string WriteableSupersededRow = "WRITEABLE_SUPERSEDED_ROW";
        public const string SupersededWithNonzeroAvailable = "SUPERSEDED_WITH_NONZERO_AVAILABLE";
        public const string SupersededWithNonzeroReserved = "SUPERSEDED_WITH_NONZERO_RESERVED";
        public const string SupersessionTargetMissing = "SUPERSESSION_TARGET_MISSING";
        public const string ChildRecipeMappingMissing = "CHILD_RECIPE_MAPPING_MISSING";
        public const string InvalidRecipeOutputContract = "INVALID_RECIPE_OUTPUT_CONTRACT";
        public const string OutputUnitConversionMissing = "OUTPUT_UNIT_CONVERSION_MISSING";
        public const string ConsolidationEvidenceMissing = "CONSOLIDATION_EVIDENCE_MISSING";
        public const string ConsolidationEvidenceWrongStore = "CONSOLIDATION_EVIDENCE_WRONG_STORE";
        public const string ConsolidationEvidenceStaleContract = "CONSOLIDATION_EVIDENCE_STALE_CONTRACT";
        public const string ConsolidationEvidenceNotCompleted = "CONSOLIDATION_EVIDENCE_NOT_COMPLETED";
        public const string ConsolidationReportUnresolved = "CONSOLIDATION_REPORT_UNRESOLVED";
        public const string LegacyRowCreatedAfterEvidence = "LEGACY_ROW_CREATED_AFTER_EVIDENCE";
        public const string InventoryChangedAfterNoopEvidence = "INVENTORY_CHANGED_AFTER_NOOP_EVIDENCE";
        public const string ProductionMovementRecipeIdentity = "PRODUCTION_MOVEMENT_RECIPE_IDENTITY";
        public const string ProductionOutputBatchCountSuspect = "PRODUCTION_OUTPUT_BATCH_COUNT_SUSPECT";
        public const string ProductionOutputQuantityMismatch = "PRODUCTION_OUTPUT_QUANTITY_MISMATCH";
        public const string ProductionMovementWithoutRunLink = "PRODUCTION_MOVEMENT_WITHOUT_RUN_LINK";
        public const string ProductionWriteToSupersededRow = "PRODUCTION_WRITE_TO_SUPERSEDED_ROW";
        public const string ProductionOutputNotCanonical = "PRODUCTION_OUTPUT_NOT_CANONICAL";
        public const string PosSalesMovementRecipeOnlyIdentity = "POS_SALES_MOVEMENT_RECIPE_ONLY_IDENTITY";
        public const string PosMovementWithoutReferenceOrder = "POS_MOVEMENT_WITHOUT_REFERENCE_ORDER";
        public const string PosWriteToSupersededRow = "POS_WRITE_TO_SUPERSEDED_ROW";
        public const string DuplicateSalesDeductionSet = "DUPLICATE_SALES_DEDUCTION_SET";
        public const string OpenRecipeOnlyBtpAlertAfterCutover = "OPEN_RECIPE_ONLY_BTP_ALERT_AFTER_CUTOVER";
        public const string SubmittedRecipeOnlyBtpRestockAfterCutover = "SUBMITTED_RECIPE_ONLY_BTP_RESTOCK_AFTER_CUTOVER";
        public const string DuplicateOpenPreparedItemAlert = "DUPLICATE_OPEN_PREPARED_ITEM_ALERT";
        public const string AlertIdentityCollision = "ALERT_IDENTITY_COLLISION";
        public const string RestockIdentityCollision = "RESTOCK_IDENTITY_COLLISION";
        public const string RequiredCapabilityMissing = "REQUIRED_CAPABILITY_MISSING";
        public const string RequiredCapabilityNotReady = "REQUIRED_CAPABILITY_NOT_READY";
        public const string SchemaContractNotReady = "SCHEMA_CONTRACT_NOT_READY";
        public const string AuditTimestampInsufficient = "AUDIT_TIMESTAMP_INSUFFICIENT";
    }

    public sealed class InventorySchemaReadinessReport
    {
        public bool IsReady { get; init; }
        public string ContractVersion { get; init; } = CutoverContractVersions.Schema;
        public string ContractHash { get; init; } = string.Empty;
        public DateTime CheckedAtUtc { get; init; }
        public IReadOnlyList<string> MissingTables { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> MissingColumns { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> MissingIndexes { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> MissingForeignKeys { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> MissingOrIncorrectChecks { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
        public string? FailureCode { get; init; }
    }

    public sealed class CutoverAnomaly
    {
        public string Code { get; init; } = string.Empty;
        public string EvidenceId { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }

    public sealed class CutoverReconciliationReport
    {
        public int StoreId { get; init; }
        public string ReconciliationContractVersion { get; init; } = CutoverContractVersions.Reconciliation;
        public string EnvironmentFingerprint { get; init; } = string.Empty;
        public DateTime GeneratedAtUtc { get; init; }
        public InventoryWriterMode WriterMode { get; init; }
        public bool HasEverActivatedPreparedItem { get; init; }
        public string ConfigRowVersionHex { get; init; } = string.Empty;
        public InventorySchemaReadinessReport Schema { get; init; } = new();
        public IReadOnlyList<InventoryWriterCapabilityStatus> Capabilities { get; init; }
            = Array.Empty<InventoryWriterCapabilityStatus>();
        public int? ConsolidationEvidenceRunId { get; init; }
        public string? ConsolidationEvidenceHash { get; init; }
        public int TotalBtpRows { get; init; }
        public int CanonicalCount { get; init; }
        public int LegacyCount { get; init; }
        public int SupersededCount { get; init; }
        public IReadOnlyList<CutoverAnomaly> Anomalies { get; init; } = Array.Empty<CutoverAnomaly>();
        public IReadOnlyDictionary<string, int> AnomalyCounts { get; init; }
            = new Dictionary<string, int>();
        public bool IsClean { get; init; }
        public string ReconciliationHash { get; init; } = string.Empty;
        public string ReadinessHash { get; init; } = string.Empty;
        public int? LatestActivationTransitionId { get; init; }
        public DateTime? LatestActivationAtUtc { get; init; }
        public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();
    }

    public sealed class CutoverActivationRequest
    {
        public int StoreId { get; init; }
        public Guid RequestKey { get; init; }
        public InventoryWriterMode TargetMode { get; init; } = InventoryWriterMode.PreparedItem;
        public InventoryWriterMode ExpectedMode { get; init; }
        public byte[] ExpectedRowVersion { get; init; } = Array.Empty<byte>();
        public string ExpectedReadinessHash { get; init; } = string.Empty;
        public string ExpectedReconciliationHash { get; init; } = string.Empty;
        public string ExpectedSchemaContractHash { get; init; } = string.Empty;
        public bool MaintenanceWindowAcknowledged { get; init; }
        public string Reason { get; init; } = string.Empty;
        public int ActorAccountId { get; init; }
    }

    public sealed class CutoverActivationResult
    {
        public bool Succeeded { get; init; }
        public bool WasReplay { get; init; }
        public string Message { get; init; } = string.Empty;
        public string? FailureCode { get; init; }
        public InventoryWriterModeStatusDto? Status { get; init; }
        public CutoverReconciliationReport? Reconciliation { get; init; }
        public InventoryWriterReadinessReport? Readiness { get; init; }
        public int? TransitionId { get; init; }
    }

    public sealed class CutoverGraduationSummary
    {
        public DateTime GeneratedAtUtc { get; init; }
        public bool AllActiveStoresPreparedItem { get; init; }
        public bool NoLegacyOrBlockedStores { get; init; }
        public bool SchemaReady { get; init; }
        public bool AllStoresHaveCleanReconciliation { get; init; }
        public bool AllStoresHaveConsolidationEvidence { get; init; }
        public bool GlobalLegacyDisableEnabled { get; init; }
        public bool EligibleToCloseUmbrella114 { get; init; }
        public IReadOnlyList<int> StoreIdsNotPrepared { get; init; } = Array.Empty<int>();
        public IReadOnlyList<string> Blockers { get; init; } = Array.Empty<string>();
        public string Note { get; init; } =
            "Umbrella #114 requires operator confirmation on target environment; never auto-closed from local/test evidence.";
    }

    /// <summary>JSON payload stored in InventoryWriterModeTransition.ReadinessSnapshotJson for cutover activations.</summary>
    public sealed class CutoverActivationEvidenceDocument
    {
        public string Kind { get; set; } = "CutoverActivation";
        public Guid RequestKey { get; set; }
        public string ReconciliationContractVersion { get; set; } = CutoverContractVersions.Reconciliation;
        public string SchemaContractVersion { get; set; } = CutoverContractVersions.Schema;
        public string ReconciliationHash { get; set; } = string.Empty;
        public string ReadinessHash { get; set; } = string.Empty;
        public string SchemaContractHash { get; set; } = string.Empty;
        public string EnvironmentFingerprint { get; set; } = string.Empty;
        public DateTime GeneratedAtUtc { get; set; }
        public bool IsClean { get; set; }
        public int AnomalyCount { get; set; }
        public int? ConsolidationEvidenceRunId { get; set; }
        public bool MaintenanceWindowAcknowledged { get; set; }
        public IReadOnlyList<InventoryWriterCapabilityStatus>? Capabilities { get; set; }
        public Dictionary<string, int>? AnomalyCounts { get; set; }
    }
}
