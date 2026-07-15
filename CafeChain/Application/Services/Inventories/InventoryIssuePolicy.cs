using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;

namespace CafeChain.Application.Services.Inventories;

public sealed class InventoryIssuePolicy : IInventoryIssuePolicy
{
    private static readonly HashSet<string> AllowedManualPurposes =
        new(StringComparer.OrdinalIgnoreCase) { "SALE", "GIFT", "DEBT", "SAMPLE" };

    private readonly IInventoryIssueSettingsProvider _settingsProvider;

    public InventoryIssuePolicy(IInventoryIssueSettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;
    }

    public async Task<InventoryIssueDecision> EvaluateAsync(
        InventoryIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        var projectedAfter = request.BeforeAvailableQty - request.IssueQty;
        if (request.IssueQty <= 0)
            return Blocked(request, projectedAfter, 0, InventoryIssueReasonCodes.InvalidIssueQuantity, string.Empty);

        if ((request.IngredientId.HasValue && request.PreparedItemId.HasValue)
            || (!request.IngredientId.HasValue && !request.PreparedItemId.HasValue))
        {
            return Blocked(request, projectedAfter, 0, InventoryIssueReasonCodes.InvalidInventoryIdentity, string.Empty);
        }

        if (projectedAfter >= 0)
            return Allowed(request, projectedAfter, 0, InventoryIssueReasonCodes.NonNegativeIssueAllowed, request.PolicyVersion ?? string.Empty);

        if (request.Operation == InventoryIssueOperation.PosBlindSale)
            return Allowed(request, projectedAfter, 0, InventoryIssueReasonCodes.PosBlindSaleAllowed, "pos-adr-0001");

        var strictReason = request.Operation switch
        {
            InventoryIssueOperation.AdjustmentOut => InventoryIssueReasonCodes.AdjustmentOutNegativeForbidden,
            InventoryIssueOperation.Waste => InventoryIssueReasonCodes.WasteNegativeForbidden,
            InventoryIssueOperation.ProductionOut => InventoryIssueReasonCodes.ProductionOutNegativeForbidden,
            InventoryIssueOperation.TransferDispatch => InventoryIssueReasonCodes.TransferSourceNegativeForbidden,
            _ => null
        };
        if (strictReason != null)
            return Blocked(request, projectedAfter, 0, strictReason, request.PolicyVersion ?? string.Empty);

        if (request.Operation != InventoryIssueOperation.ManualExternalExport
            || !AllowedManualPurposes.Contains(request.DocumentPurpose?.Trim() ?? string.Empty))
        {
            return Blocked(request, projectedAfter, 0, InventoryIssueReasonCodes.ManualNegativePurposeNotAllowed, request.PolicyVersion ?? string.Empty);
        }

        var settings = await _settingsProvider.GetManualExternalExportSettingsAsync(cancellationToken);
        if (!settings.IsValid)
            return Blocked(request, projectedAfter, 0, InventoryIssueReasonCodes.NegativeSettingInvalid, settings.PolicyVersion);
        if (!settings.Enabled)
            return Blocked(request, projectedAfter, 0, InventoryIssueReasonCodes.ManualNegativeFeatureDisabled, settings.PolicyVersion);
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Blocked(request, projectedAfter, 0, InventoryIssueReasonCodes.ManualNegativeReasonRequired, settings.PolicyVersion);

        var effectiveLimit = request.ItemMaxNegativeQty ?? settings.DefaultMaxNegativeQuantity;
        if (effectiveLimit < 0)
            return Blocked(request, projectedAfter, 0, InventoryIssueReasonCodes.NegativeSettingInvalid, settings.PolicyVersion);
        if (projectedAfter < -effectiveLimit)
            return Blocked(request, projectedAfter, effectiveLimit, InventoryIssueReasonCodes.ManualNegativeLimitExceeded, settings.PolicyVersion);

        var approval = request.Approval;
        if (approval == null || !approval.IsApproved)
            return ApprovalRequired(request, projectedAfter, effectiveLimit, settings.PolicyVersion);

        if (string.Equals(approval.RequesterId, approval.ApproverId, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(approval.RequesterId))
        {
            return Blocked(request, projectedAfter, effectiveLimit, InventoryIssueReasonCodes.SelfApprovalForbidden, settings.PolicyVersion);
        }
        if (!approval.IsScopeAuthorized)
            return Blocked(request, projectedAfter, effectiveLimit, InventoryIssueReasonCodes.ApprovalScopeForbidden, settings.PolicyVersion);
        if (!MatchesApproval(request, approval, projectedAfter, effectiveLimit, settings.PolicyVersion))
            return Blocked(request, projectedAfter, effectiveLimit, InventoryIssueReasonCodes.ApprovalStale, settings.PolicyVersion);

        return Allowed(request, projectedAfter, effectiveLimit, InventoryIssueReasonCodes.NonNegativeIssueAllowed, settings.PolicyVersion);
    }

    private static bool MatchesApproval(
        InventoryIssueRequest request,
        InventoryApprovalEvidence approval,
        decimal projectedAfter,
        decimal effectiveLimit,
        string policyVersion) =>
        approval.StoreId == request.StoreId
        && approval.IngredientId == request.IngredientId
        && approval.PreparedItemId == request.PreparedItemId
        && approval.BeforeQty == request.BeforeAvailableQty
        && approval.ApprovedIssueQty == request.IssueQty
        && approval.ProjectedAfterQty == projectedAfter
        && approval.EffectiveMaxNegativeQty == effectiveLimit
        && string.Equals(approval.ApprovedReason.Trim(), request.Reason?.Trim(), StringComparison.Ordinal)
        && (approval.InventoryRowVersion == null
            || request.InventoryRowVersion == null
            || approval.InventoryRowVersion.SequenceEqual(request.InventoryRowVersion))
        && string.Equals(approval.PolicyVersion, policyVersion, StringComparison.Ordinal);

    private static InventoryIssueDecision Allowed(
        InventoryIssueRequest request, decimal after, decimal limit, string reason, string version) =>
        Decision(InventoryIssueOutcome.Allowed, request, after, limit, reason, version);

    private static InventoryIssueDecision ApprovalRequired(
        InventoryIssueRequest request, decimal after, decimal limit, string version) =>
        Decision(InventoryIssueOutcome.ApprovalRequired, request, after, limit,
            InventoryIssueReasonCodes.ManualNegativeApprovalRequired, version);

    private static InventoryIssueDecision Blocked(
        InventoryIssueRequest request, decimal after, decimal limit, string reason, string version) =>
        Decision(InventoryIssueOutcome.Blocked, request, after, limit, reason, version);

    private static InventoryIssueDecision Decision(
        InventoryIssueOutcome outcome,
        InventoryIssueRequest request,
        decimal after,
        decimal limit,
        string reason,
        string version) =>
        new(
            outcome,
            reason,
            request.BeforeAvailableQty,
            request.IssueQty,
            after,
            limit,
            Math.Abs(Math.Min(after, 0)),
            after < 0,
            outcome == InventoryIssueOutcome.ApprovalRequired,
            version);
}
