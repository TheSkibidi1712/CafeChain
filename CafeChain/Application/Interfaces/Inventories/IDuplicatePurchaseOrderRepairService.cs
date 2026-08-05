using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories;

public interface IDuplicatePurchaseOrderRepairService
{
    Task<DuplicatePurchaseOrderRepairReport> DryRunAsync();
    Task<ServiceResult<DuplicatePurchaseOrderRepairReport>> ExecuteAsync(AdminActorContext actor);
}

public static class DuplicatePurchaseOrderRepairStatuses
{
    public const string SafeToCancel = "SAFE_TO_CANCEL";
    public const string ManualReviewRequired = "MANUAL_REVIEW_REQUIRED";
}

public sealed class DuplicatePurchaseOrderRepairReport
{
    public int SafeToCancelCount { get; set; }
    public int ManualReviewCount { get; set; }
    public int CancelledCount { get; set; }
    public IReadOnlyList<DuplicatePurchaseOrderRepairItem> Items { get; set; } = Array.Empty<DuplicatePurchaseOrderRepairItem>();
}

public sealed class DuplicatePurchaseOrderRepairItem
{
    public string Status { get; set; } = string.Empty;
    public int PurchaseAdviceLineId { get; set; }
    public string PurchaseAdviceNumber { get; set; } = string.Empty;
    public int PurchaseOrderBatchId { get; set; }
    public string PurchaseOrderBatchNumber { get; set; } = string.Empty;
    public string PurchaseOrderCode { get; set; } = string.Empty;
    public decimal AllocatedBaseQuantity { get; set; }
    public string Message { get; set; } = string.Empty;
}
