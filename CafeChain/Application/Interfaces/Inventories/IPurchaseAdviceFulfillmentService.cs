using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories;

public interface IPurchaseAdviceFulfillmentService
{
    Task<ServiceResult> BackPostAcceptedAsync(
        int purchaseOrderLineId,
        int branchReceiptLineId,
        decimal acceptedQuantity,
        int actorStaffId);

    Task<ServiceResult> BackPostClosedAsync(
        int purchaseOrderLineId,
        decimal closedQuantity,
        string closeOperationKey,
        string payloadHash,
        int actorStaffId);

    Task<PurchaseAdviceCloseReplay?> FindClosedReplayAsync(string closeOperationKey);

    Task RecomputeHeaderStatusAsync(int purchaseAdviceId, int actorStaffId, string reason);

    Task<PurchaseAdviceBackfillDryRunReport> BuildBackfillDryRunReportAsync();
}

public sealed class PurchaseAdviceCloseReplay
{
    public int PurchaseOrderLineId { get; init; }
    public decimal Quantity { get; init; }
    public string PayloadHash { get; init; } = string.Empty;
}

public static class PurchaseAdviceBackfillStatuses
{
    public const string Ready = "READY";
    public const string AlreadyPosted = "ALREADY_POSTED";
    public const string ManualReviewRequired = "MANUAL_REVIEW_REQUIRED";
    public const string AggregateDrift = "AGGREGATE_DRIFT";
}

public sealed class PurchaseAdviceBackfillDryRunReport
{
    public int AcceptedCandidateCount { get; set; }
    public decimal AcceptedCandidateQuantity { get; set; }
    public int ClosedCandidateCount { get; set; }
    public decimal ClosedCandidateQuantity { get; set; }
    public int ExistingPostingCount { get; set; }
    public int ManualReviewCount { get; set; }
    public IReadOnlyList<PurchaseAdviceBackfillDryRunItem> Items { get; set; } = Array.Empty<PurchaseAdviceBackfillDryRunItem>();
}

public sealed class PurchaseAdviceBackfillDryRunItem
{
    public string Status { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public int SourceDocumentId { get; set; }
    public int SourceDocumentLineId { get; set; }
    public int PurchaseOrderLineId { get; set; }
    public int? PurchaseAdviceLineId { get; set; }
    public decimal Quantity { get; set; }
    public string Message { get; set; } = string.Empty;
}
