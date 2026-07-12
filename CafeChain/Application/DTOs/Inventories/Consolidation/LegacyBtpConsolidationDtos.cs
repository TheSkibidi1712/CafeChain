using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Application.DTOs.Inventories.Consolidation
{
    public static class LegacyBtpConsolidationConstants
    {
        public const string QueryContractVersion = "123.1";
        public const string ManifestVersion = "123.1";
        public const string AuditCriteriaVersion = "123.1";
    }

    public static class ConsolidationFailureCodes
    {
        public const string StoreNotFound = "STORE_NOT_FOUND";
        public const string ManifestStoreMismatch = "MANIFEST_STORE_MISMATCH";
        public const string SourceNotFound = "SOURCE_NOT_FOUND";
        public const string SourceAlreadySuperseded = "SOURCE_ALREADY_SUPERSEDED";
        public const string SourceMappingMissing = "SOURCE_MAPPING_MISSING";
        public const string SourcePreparedItemMismatch = "SOURCE_PREPARED_ITEM_MISMATCH";
        public const string TargetNotFound = "TARGET_NOT_FOUND";
        public const string TargetNotCanonical = "TARGET_NOT_CANONICAL";
        public const string TargetCollision = "TARGET_COLLISION";
        public const string MultipleCanonicalTargets = "MULTIPLE_CANONICAL_TARGETS";
        public const string UnknownQuantitySemantics = "UNKNOWN_QUANTITY_SEMANTICS";
        public const string UnitMismatch = "UNIT_MISMATCH";
        public const string ConversionEvidenceMissing = "CONVERSION_EVIDENCE_MISSING";
        public const string ThresholdDecisionMissing = "THRESHOLD_DECISION_MISSING";
        public const string AlertIdentityCollision = "ALERT_IDENTITY_COLLISION";
        public const string RestockIdentityCollision = "RESTOCK_IDENTITY_COLLISION";
        public const string StaleManifest = "STALE_MANIFEST";
        public const string StoreNotBlockedForExecute = "STORE_NOT_BLOCKED_FOR_EXECUTE";
        public const string ConsolidationStoreNotBlocked = "CONSOLIDATION_STORE_NOT_BLOCKED";
        public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
        public const string QuantityPrecisionLoss = "QUANTITY_PRECISION_LOSS";
        public const string ConservationFailed = "CONSERVATION_FAILED";
        public const string UnauthorizedExecute = "UNAUTHORIZED_EXECUTE";
        public const string RunNotDryRunReady = "RUN_NOT_DRY_RUN_READY";
        public const string DryRunHashMismatch = "DRY_RUN_HASH_MISMATCH";
        public const string ExecutingInProgress = "EXECUTING_IN_PROGRESS";
        public const string NoOpNotEligible = "NO_OP_NOT_ELIGIBLE";
        public const string ExplicitApprovalRequired = "EXPLICIT_APPROVAL_REQUIRED";
        public const string InvalidManifest = "INVALID_MANIFEST";
        public const string TargetSpecMissing = "TARGET_SPEC_MISSING";
        public const string TargetSpecAmbiguous = "TARGET_SPEC_AMBIGUOUS";
    }

    public sealed class ConsolidationAuditRowDto
    {
        public int StoreInventoryId { get; init; }
        public int? RecipeId { get; init; }
        public int? PreparedItemId { get; init; }
        public string Classification { get; init; } = string.Empty;
        public BtpIdentityState? IdentityState { get; init; }
        public InventoryQuantitySemanticsStatus? QuantitySemantics { get; init; }
        public decimal AvailableQty { get; init; }
        public decimal ReservedQty { get; init; }
        public decimal? MinStockLevel { get; init; }
        public decimal? MaxNegativeQty { get; init; }
        public int? SupersededByStoreInventoryId { get; init; }
        public string? UnitEvidence { get; init; }
        public string? BlockerCode { get; init; }
        public string? BlockerReason { get; init; }
        public string RowFingerprint { get; init; } = string.Empty;
    }

    public sealed class ConsolidationAuditReportDto
    {
        public int StoreId { get; init; }
        public string EnvironmentFingerprint { get; init; } = string.Empty;
        public string QueryContractVersion { get; init; } = string.Empty;
        public string AuditCriteriaVersion { get; init; } = string.Empty;
        public DateTime AuditedAtUtc { get; init; }
        public string AuditHash { get; init; } = string.Empty;
        public bool IsNoOpEligible { get; init; }
        public int TotalBtpRows { get; init; }
        public int RecipeOnlyLegacyCount { get; init; }
        public int CompatibilityCount { get; init; }
        public int CanonicalCount { get; init; }
        public int NonCanonicalPreparedOnlyCount { get; init; }
        public int SupersededCount { get; init; }
        public int UnknownSemanticsCount { get; init; }
        public int CollisionCount { get; init; }
        public int UnmappedRecipeCount { get; init; }
        public int MultipleCanonicalCandidateCount { get; init; }
        public int UnitMismatchCount { get; init; }
        public int ThresholdConflictGroupCount { get; init; }
        public IReadOnlyList<ConsolidationAuditRowDto> Rows { get; init; }
            = Array.Empty<ConsolidationAuditRowDto>();
        public IReadOnlyList<string> BlockerCodes { get; init; }
            = Array.Empty<string>();
        public IReadOnlyDictionary<int, IReadOnlyList<int>> PreparedItemGroups { get; init; }
            = new Dictionary<int, IReadOnlyList<int>>();
    }

    public sealed class ConsolidationConversionEvidenceDto
    {
        public int FromUnitId { get; init; }
        public int ToUnitId { get; init; }
        public decimal Factor { get; init; }
        public string SourceReference { get; init; } = string.Empty;
        public int ApproverStaffId { get; init; }
        public string Version { get; init; } = "1";
    }

    public sealed class ConsolidationGroupManifestDto
    {
        public int StoreId { get; init; }
        public int PreparedItemId { get; init; }
        public IReadOnlyList<int> SourceStoreInventoryIds { get; init; } = Array.Empty<int>();
        public int? TargetStoreInventoryId { get; init; }
        public bool CreateCanonicalTarget { get; init; }
        public decimal ApprovedMinStockLevel { get; init; }
        public decimal? ApprovedMaxNegativeQty { get; init; }
        public bool ThresholdDecisionProvided { get; init; }
        public IReadOnlyDictionary<int, ConsolidationConversionEvidenceDto>? ConversionBySourceId { get; init; }
        public string QuantitySemanticsEvidence { get; init; } = string.Empty;
        public string EvidenceReference { get; init; } = string.Empty;
        public string? Notes { get; init; }
        public int ActorApprovalStaffId { get; init; }
        /// <summary>
        /// When true, allow dual OPEN Recipe+PI alerts for the group by explicit owner decision
        /// (default false → ALERT_IDENTITY_COLLISION).
        /// </summary>
        public bool AllowAlertIdentityCollision { get; init; }
    }

    public sealed class ConsolidationManifestDto
    {
        public string ManifestVersion { get; init; } = LegacyBtpConsolidationConstants.ManifestVersion;
        public string QueryContractVersion { get; init; } = LegacyBtpConsolidationConstants.QueryContractVersion;
        public int StoreId { get; init; }
        public IReadOnlyList<ConsolidationGroupManifestDto> Groups { get; init; }
            = Array.Empty<ConsolidationGroupManifestDto>();
    }

    public sealed class ConsolidationDryRunRequest
    {
        public int StoreId { get; init; }
        public Guid RequestKey { get; init; }
        public ConsolidationManifestDto Manifest { get; init; } = null!;
        public int RequestedByStaffId { get; init; }
        public int? ApprovedByStaffId { get; init; }
    }

    public sealed class ConsolidationExecuteRequest
    {
        public int StoreId { get; init; }
        public Guid RequestKey { get; init; }
        public string ExpectedDryRunHash { get; init; } = string.Empty;
        public int ExecutedByStaffId { get; init; }
        public string ActorRole { get; init; } = string.Empty;
    }

    public sealed class ConsolidationNoOpRequest
    {
        public int StoreId { get; init; }
        public Guid RequestKey { get; init; }
        public int RequestedByStaffId { get; init; }
        public int ApprovedByStaffId { get; init; }
        public bool ExplicitApproval { get; init; }
        public string ExpectedAuditHash { get; init; } = string.Empty;
    }

    public sealed class ConsolidationRunResultDto
    {
        public int InventoryConsolidationRunId { get; init; }
        public int StoreId { get; init; }
        public Guid RequestKey { get; init; }
        public InventoryConsolidationRunType RunType { get; init; }
        public InventoryConsolidationRunStatus Status { get; init; }
        public string ManifestHash { get; init; } = string.Empty;
        public string? DryRunHash { get; init; }
        public string QueryContractVersion { get; init; } = string.Empty;
        public string EnvironmentFingerprint { get; init; } = string.Empty;
        public bool WasReplay { get; init; }
        public string? FailureCode { get; init; }
        public string? FailureDetails { get; init; }
        public decimal BeforeAvailableTotal { get; init; }
        public decimal BeforeReservedTotal { get; init; }
        public decimal AfterAvailableTotal { get; init; }
        public decimal AfterReservedTotal { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? DryRunAt { get; init; }
        public DateTime? CompletedAt { get; init; }
        public string? ReportJson { get; init; }
        public ConsolidationAuditReportDto? AuditReport { get; init; }
    }
}
