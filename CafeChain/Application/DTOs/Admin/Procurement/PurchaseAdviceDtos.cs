using CafeChain.Application.DTOs.Admin.Actor;

namespace CafeChain.Application.DTOs.Admin.Procurement
{
    public sealed class PurchaseAdviceFilterDto
    {
        public int? StoreId { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public int? IngredientId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public sealed class PurchaseAdviceListItemDto
    {
        public int PurchaseAdviceId { get; set; }
        public string AdviceNumber { get; set; } = string.Empty;
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string RequestedByName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public DateTime NeededByDate { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public int LineCount { get; set; }
        public decimal TotalRequestedBaseQuantity { get; set; }
        public string SourceRestockSummary { get; set; } = string.Empty;
    }

    public class PurchaseAdviceSourceDto
    {
        public int RestockRequestId { get; set; }
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public int IngredientId { get; set; }
        public string IngredientName { get; set; } = string.Empty;
        public int BaseUnitId { get; set; }
        public string BaseUnitName { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public decimal RestockRequestedQuantity { get; set; }
        public decimal? RestockRequestedProcurementQuantity { get; set; }
        public int? ProcurementUnitId { get; set; }
        public string? ProcurementUnitName { get; set; }
        public decimal TransferAllocatedQuantity { get; set; }
        public decimal ExistingPurchaseAdviceQuantity { get; set; }
        public decimal ExistingPurchaseOrderQuantity { get; set; }
        public decimal ExplicitlyClosedQuantity { get; set; }
        public decimal RemainingToPurchaseQuantity { get; set; }
        public decimal? TransferAllocatedProcurementQuantity { get; set; }
        public decimal? ExistingPurchaseAdviceProcurementQuantity { get; set; }
        public decimal? ExistingPurchaseOrderProcurementQuantity { get; set; }
        public decimal? ExplicitlyClosedProcurementQuantity { get; set; }
        public decimal? RemainingToPurchaseProcurementQuantity { get; set; }
        public decimal? PendingPurchaseAllocationProcurementQuantity { get; set; }
        public decimal PendingPurchaseAllocationBaseQuantity { get; set; }
        public string RestockRowVersion { get; set; } = string.Empty;
    }

    public sealed class CreatePurchaseAdviceRequest
    {
        public int StoreId { get; set; }
        public string RequestKey { get; set; } = string.Empty;
        public bool IsDirectProposal { get; set; }
        public DateTime NeededByDate { get; set; }
        public string Priority { get; set; } = string.Empty;
        public string? Note { get; set; }
        public List<CreatePurchaseAdviceLineRequest> Lines { get; set; } = new();
    }

    public sealed class CreatePurchaseAdviceLineRequest
    {
        public int? RestockRequestId { get; set; }
        public int? IngredientId { get; set; }
        public decimal? RequestedProcurementQuantity { get; set; }
        public int? ProcurementUnitId { get; set; }
        public decimal RequestedPurchaseBaseQuantity { get; set; }
        public decimal? RequestedPurchaseProcurementQuantity { get; set; }
        public DateTime? NeededByDate { get; set; }
        public string? Note { get; set; }
        public string RestockRowVersion { get; set; } = string.Empty;
    }

    public sealed class UpdatePurchaseAdviceRequest
    {
        public int PurchaseAdviceId { get; set; }
        public DateTime NeededByDate { get; set; }
        public string Priority { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string RowVersion { get; set; } = string.Empty;
        public List<UpdatePurchaseAdviceLineRequest> Lines { get; set; } = new();
    }

    public sealed class UpdatePurchaseAdviceLineRequest
    {
        public int PurchaseAdviceLineId { get; set; }
        public decimal RequestedPurchaseBaseQuantity { get; set; }
        public decimal? RequestedPurchaseProcurementQuantity { get; set; }
        public DateTime? NeededByDate { get; set; }
        public string? Note { get; set; }
        public string RowVersion { get; set; } = string.Empty;
    }

    public sealed class PurchaseAdviceTransitionRequest
    {
        public string RowVersion { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }

    public sealed class AddRestockRequestToDraftPurchaseAdviceRequest
    {
        public int PurchaseAdviceId { get; set; }
        public int RestockRequestId { get; set; }
        public string PurchaseAdviceRowVersion { get; set; } = string.Empty;
        public string RestockRowVersion { get; set; } = string.Empty;
    }

    public sealed class PurchaseAdviceDetailDto
    {
        public int PurchaseAdviceId { get; set; }
        public string AdviceNumber { get; set; } = string.Empty;
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public int RequestedByStaffId { get; set; }
        public string RequestedByName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public DateTime NeededByDate { get; set; }
        public string? Note { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? SubmittedAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string RowVersion { get; set; } = string.Empty;
        public List<PurchaseAdviceLineDto> Lines { get; set; } = new();
        public List<PurchaseAdviceTransitionDto> Transitions { get; set; } = new();
        public bool CanEdit { get; set; }
        public bool CanSubmit { get; set; }
        public bool CanCancel { get; set; }
        public bool CanReview { get; set; }
        public bool CanReject { get; set; }
    }

    public sealed class PurchaseAdviceLineDto : PurchaseAdviceSourceDto
    {
        public int PurchaseAdviceLineId { get; set; }
        public decimal RequestedPurchaseBaseQuantity { get; set; }
        public decimal? RequestedProcurementQuantity { get; set; }
        public decimal AllocatedToPoProcurementQuantity { get; set; }
        public decimal AcceptedProcurementQuantity { get; set; }
        public decimal ClosedProcurementQuantity { get; set; }
        public decimal AllocatedToPoBaseQuantity { get; set; }
        public decimal AcceptedBaseQuantity { get; set; }
        public decimal ClosedBaseQuantity { get; set; }
        public decimal RemainingToOrderQuantity { get; set; }
        public decimal RemainingToReceiveQuantity { get; set; }
        public decimal UnresolvedQuantity { get; set; }
        public decimal? RemainingToOrderProcurementQuantity { get; set; }
        public decimal? RemainingToReceiveProcurementQuantity { get; set; }
        public decimal? UnresolvedProcurementQuantity { get; set; }
        public string LineStatus { get; set; } = string.Empty;
        public DateTime NeededByDate { get; set; }
        public string? Note { get; set; }
        public string RowVersion { get; set; } = string.Empty;
    }

    public sealed class PurchaseAdviceTransitionDto
    {
        public string? PreviousStatus { get; set; }
        public string NewStatus { get; set; } = string.Empty;
        public string ActorName { get; set; } = string.Empty;
        public DateTime OccurredAtUtc { get; set; }
        public string? Reason { get; set; }
    }

    public sealed class PurchaseAdvicePageDto
    {
        public PurchaseAdviceFilterDto Filter { get; set; } = new();
        public IReadOnlyList<PurchaseAdviceListItemDto> Items { get; set; } = Array.Empty<PurchaseAdviceListItemDto>();
        public IReadOnlyList<PurchaseAdviceSourceDto> AvailableSources { get; set; } = Array.Empty<PurchaseAdviceSourceDto>();
        public IReadOnlyList<(int StoreId, string StoreName)> Stores { get; set; } = Array.Empty<(int, string)>();
        public AdminActorContext Actor { get; set; } = new();
    }
}
