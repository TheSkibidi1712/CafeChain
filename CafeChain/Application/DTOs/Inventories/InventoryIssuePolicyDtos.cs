namespace CafeChain.Application.DTOs.Inventories;

public enum InventoryIssueOperation
{
    PosBlindSale = 1,
    ManualExternalExport = 2,
    AdjustmentOut = 3,
    Waste = 4,
    ProductionOut = 5,
    TransferDispatch = 6
}

public enum InventoryIssueOutcome
{
    Allowed = 1,
    ApprovalRequired = 2,
    Blocked = 3
}

public static class InventoryIssueReasonCodes
{
    public const string NonNegativeIssueAllowed = "NON_NEGATIVE_ISSUE_ALLOWED";
    public const string PosBlindSaleAllowed = "POS_BLIND_SALE_ALLOWED";
    public const string InvalidIssueQuantity = "INVALID_ISSUE_QUANTITY";
    public const string InvalidInventoryIdentity = "INVALID_INVENTORY_IDENTITY";
    public const string ManualNegativeFeatureDisabled = "MANUAL_NEGATIVE_FEATURE_DISABLED";
    public const string ManualNegativePurposeNotAllowed = "MANUAL_NEGATIVE_PURPOSE_NOT_ALLOWED";
    public const string ManualNegativeReasonRequired = "MANUAL_NEGATIVE_REASON_REQUIRED";
    public const string ManualNegativeApprovalRequired = "MANUAL_NEGATIVE_APPROVAL_REQUIRED";
    public const string ManualNegativeLimitExceeded = "MANUAL_NEGATIVE_LIMIT_EXCEEDED";
    public const string NegativeSettingInvalid = "NEGATIVE_SETTING_INVALID";
    public const string AdjustmentOutNegativeForbidden = "ADJUSTMENT_OUT_NEGATIVE_FORBIDDEN";
    public const string WasteNegativeForbidden = "WASTE_NEGATIVE_FORBIDDEN";
    public const string ProductionOutNegativeForbidden = "PRODUCTION_OUT_NEGATIVE_FORBIDDEN";
    public const string TransferSourceNegativeForbidden = "TRANSFER_SOURCE_NEGATIVE_FORBIDDEN";
    public const string ApprovalStale = "APPROVAL_STALE";
    public const string ApprovalScopeForbidden = "APPROVAL_SCOPE_FORBIDDEN";
    public const string SelfApprovalForbidden = "SELF_APPROVAL_FORBIDDEN";
}

public sealed record InventoryApprovalEvidence(
    long ApprovalId,
    int StoreId,
    int? IngredientId,
    int? PreparedItemId,
    decimal BeforeQty,
    decimal ProjectedAfterQty,
    decimal EffectiveMaxNegativeQty,
    string PolicyVersion,
    string RequesterId,
    string ApproverId,
    bool IsApproved,
    bool IsScopeAuthorized,
    decimal ApprovedIssueQty = 0,
    string ApprovedReason = "",
    byte[]? InventoryRowVersion = null);

public sealed record InventoryIssueRequest(
    InventoryIssueOperation Operation,
    int StoreId,
    int? IngredientId,
    int? PreparedItemId,
    decimal BeforeAvailableQty,
    decimal IssueQty,
    decimal? ItemMaxNegativeQty,
    string? DocumentPurpose,
    string? Reason,
    string? PolicyVersion,
    InventoryApprovalEvidence? Approval,
    byte[]? InventoryRowVersion = null);

public sealed record InventoryIssueDecision(
    InventoryIssueOutcome Outcome,
    string ReasonCode,
    decimal BeforeQty,
    decimal IssueQty,
    decimal ProjectedAfterQty,
    decimal EffectiveMaxNegativeQty,
    decimal NegativeQty,
    bool IsNegative,
    bool RequiresApproval,
    string PolicyVersion)
{
    public bool IsAllowed => Outcome == InventoryIssueOutcome.Allowed;
}

public sealed record InventoryManualNegativeSettings(
    bool IsValid,
    bool Enabled,
    bool ApprovalRequired,
    decimal DefaultMaxNegativeQuantity,
    string PolicyVersion,
    string? InvalidReasonCode = null);
