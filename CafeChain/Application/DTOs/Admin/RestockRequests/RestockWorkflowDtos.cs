using CafeChain.Application.DTOs.Admin.Procurement;

using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Application.DTOs.Admin.RestockRequests
{
    public class RestockTimelineItemDto
    {
        public int TransitionId { get; set; }
        public string PreviousStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public int ActorStaffId { get; set; }
        public string? ActorName { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string? Reason { get; set; }
        public int? BranchReceiptId { get; set; }
        public int? InventoryTransferId { get; set; }
        public int? InventoryTransactionId { get; set; }
        public decimal? QuantityBefore { get; set; }
        public decimal? QuantityAfter { get; set; }
        public string? RequestKey { get; set; }
    }

    public class RestockRequestWorkflowDetailDto : RestockRequestDetailDto
    {
        public decimal ReceivedQuantity { get; set; }
        public decimal RemainingQuantity { get; set; }
        public decimal TargetQuantity { get; set; }
        public decimal? FulfilledProcurementQuantity { get; set; }
        public decimal? TransferAllocatedProcurementQuantity { get; set; }
        public decimal? PurchaseAllocatedProcurementQuantity { get; set; }
        public decimal? ProductionAllocatedProcurementQuantity { get; set; }
        public decimal? RejectedProcurementQuantity { get; set; }
        public decimal? RemainingUnallocatedProcurementQuantity { get; set; }
        public decimal? RemainingToReceiveProcurementQuantity { get; set; }
        public decimal? ClosedProcurementQuantity { get; set; }
        public decimal FulfilledQuantity { get; set; }
        public decimal TransferAllocatedQuantity { get; set; }
        public decimal PurchaseAllocatedQuantity { get; set; }
        public decimal RemainingUnallocatedQuantity { get; set; }
        public decimal RemainingToReceiveQuantity { get; set; }
        public decimal ClosedRemainingQuantity { get; set; }
        public string FulfillmentChannel { get; set; } = string.Empty;
        public int? AcceptedByStaffId { get; set; }
        public string? AcceptedByName { get; set; }
        public DateTime? AcceptedAtUtc { get; set; }
        public string? ProcessingNote { get; set; }
        public string? RemainingCloseReason { get; set; }
        public bool StockRecoveredExternally { get; set; }
        public List<RestockTimelineItemDto> Timeline { get; set; } = new();
        public List<PurchaseOrderListItemDto> PurchaseOrders { get; set; } = new();
        public List<SupplierReceiptIssueListItemDto> SupplierIssues { get; set; } = new();
        public List<BranchReceiptListItemDto> Receipts { get; set; } = new();
        public List<RestockFulfillmentDto> Fulfillments { get; set; } = new();
        public List<RestockFulfillmentPostingDto> FulfillmentPostings { get; set; } = new();
        public List<RestockWorkflowStepDto> WorkflowSteps { get; set; } = new();
        public List<LinkedPurchaseAdviceDto> PurchaseAdvices { get; set; } = new();
        public List<DraftPurchaseAdviceOptionDto> DraftPurchaseAdvices { get; set; } = new();
        public bool HasOrphanPurchaseAllocation { get; set; }
    }

    public sealed class RestockWorkflowStepDto
    {
        public string Label { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public sealed class LinkedPurchaseAdviceDto
    {
        public int PurchaseAdviceId { get; set; }
        public int PurchaseAdviceLineId { get; set; }
        public string AdviceNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal ProcurementQuantity { get; set; }
        public string? ProcurementUnitName { get; set; }
        public string? SupplierName { get; set; }
        public bool IsActive { get; set; }
    }

    public sealed class DraftPurchaseAdviceOptionDto
    {
        public int PurchaseAdviceId { get; set; }
        public string AdviceNumber { get; set; } = string.Empty;
        public DateTime NeededByDate { get; set; }
        public string RowVersion { get; set; } = string.Empty;
    }

    public class RestockFulfillmentPostingDto
    {
        public int RestockFulfillmentPostingId { get; set; }
        public string SourceDocumentType { get; set; } = string.Empty;
        public int SourceDocumentId { get; set; }
        public int SourceDocumentLineId { get; set; }
        public decimal Quantity { get; set; }
        public int BaseUnitId { get; set; }
        public string? BaseUnitName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class RestockFulfillmentDto
    {
        public int RestockRequestFulfillmentId { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal PlannedBaseQuantity { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedByStaffId { get; set; }
    }

    public class LinkRestockFulfillmentRequest
    {
        public string SourceType { get; set; } = string.Empty;
        public decimal PlannedBaseQuantity { get; set; }
        public string? Notes { get; set; }
        public int? InventoryDocumentDetailId { get; set; }
    }

    public class BranchReceiptListItemDto
    {
        public int BranchReceiptId { get; set; }
        public int? PurchaseOrderId { get; set; }
        public string ReceiptCode { get; set; } = string.Empty;
        public string ReceiptKey { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int StoreId { get; set; }
        public int? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public DateTime ReceivedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public decimal TotalBaseQuantity { get; set; }
        public decimal TotalLineCost { get; set; }
        public int LineCount { get; set; }
    }

    public class BranchReceiptLineDto
    {
        public PurchaseMode PurchaseMode { get; set; } = PurchaseMode.Packaged;
        public int BranchReceiptLineId { get; set; }
        public int? RestockRequestId { get; set; }
        public int? PurchaseOrderLineId { get; set; }
        public int? IngredientId { get; set; }
        public int? PreparedItemId { get; set; }
        public int? RecipeId { get; set; }
        public decimal InputQuantity { get; set; }
        public int InputUnitId { get; set; }
        public string? InputUnitName { get; set; }
        public decimal ReceivedBaseQuantity { get; set; }
        public decimal RejectedBaseQuantity { get; set; }
        public decimal? ReceivedProcurementQuantity { get; set; }
        public decimal? RejectedProcurementQuantity { get; set; }
        public decimal? AcceptedProcurementQuantity { get; set; }
        public decimal? InventoryPostingBaseQuantity { get; set; }
        public int? ProcurementUnitId { get; set; }
        public string? ProcurementUnitName { get; set; }
        public decimal? ProcurementToInventoryFactor { get; set; }
        public string? RejectionReason { get; set; }
        public string? RejectionIssueType { get; set; }
        public int BaseUnitId { get; set; }
        public string? BaseUnitName { get; set; }
        public decimal? ActualPackagePrice { get; set; }
        public decimal? PackageQuantitySnapshot { get; set; }
        public int? PackageUnitIdSnapshot { get; set; }
        public decimal BaseUnitCostSnapshot { get; set; }
        public decimal LineTotalCost { get; set; }
        public int? InventoryTransactionId { get; set; }
    }

    public class BranchReceiptDetailDto : BranchReceiptListItemDto
    {
        public string RowVersion { get; set; } = string.Empty;
        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
        public int CreatedByStaffId { get; set; }
        public int? ConfirmedByStaffId { get; set; }
        public int? ReceivedByStaffId { get; set; }
        public List<BranchReceiptLineDto> Lines { get; set; } = new();
        public bool AlertEvaluationFailed { get; set; }
        public string? AlertEvaluationMessage { get; set; }
    }

    public class CreateBranchReceiptLineInput
    {
        public int RestockRequestId { get; set; }
        public int? PurchaseOrderLineId { get; set; }
        public int? RestockRequestFulfillmentId { get; set; }
        public decimal ActualReceivedQuantity { get; set; }
        public decimal RejectedQuantity { get; set; }
        public string? RejectionReason { get; set; }
        public string? RejectionIssueType { get; set; }
        public int InputUnitId { get; set; }
        /// <summary>Actual package/unit price confirmed by operator (required for post).</summary>
        public decimal ActualPackagePrice { get; set; }
        public decimal? PackageQuantity { get; set; }
        public int? PackageUnitId { get; set; }
        public int? SupplierId { get; set; }
        public int? IngredientSupplierId { get; set; }
    }

    public class CreateBranchReceiptRequest
    {
        public int StoreId { get; set; }
        public string ReceiptKey { get; set; } = string.Empty;
        public int? SupplierId { get; set; }
        public string? ReferenceNumber { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public string? Notes { get; set; }
        public List<CreateBranchReceiptLineInput> Lines { get; set; } = new();
    }

    public class ConfirmBranchReceiptResultDto
    {
        public int BranchReceiptId { get; set; }
        public string ReceiptCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool WasReplay { get; set; }
        public bool AlertEvaluationFailed { get; set; }
        public string? AlertEvaluationMessage { get; set; }
        public List<int> InventoryTransactionIds { get; set; } = new();
        public List<(int RestockRequestId, string NewStatus, decimal ReceivedQty)> RequestUpdates { get; set; } = new();
    }

    public sealed class PurchaseOrderReceiptDraftDto
    {
        public int BranchReceiptId { get; set; }
        public int PurchaseOrderId { get; set; }
        public string PurchaseOrderCode { get; set; } = string.Empty;
        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string ReceiptCode { get; set; } = string.Empty;
        public string RowVersion { get; set; } = string.Empty;
        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
        public List<PurchaseOrderReceiptDraftLineDto> Lines { get; set; } = new();
    }

    public sealed class PurchaseOrderReceiptDraftLineDto
    {
        public PurchaseMode PurchaseMode { get; set; } = PurchaseMode.Packaged;
        public int PurchaseOrderLineId { get; set; }
        public int? RestockRequestId { get; set; }
        public int IngredientId { get; set; }
        public string IngredientName { get; set; } = string.Empty;
        public string PackageUnitName { get; set; } = string.Empty;
        public decimal? PackageQuantitySnapshot { get; set; }
        public decimal? PackagePriceSnapshot { get; set; }
        public decimal? UnitPricePerProcurementUnit { get; set; }
        public decimal OrderedBaseQuantity { get; set; }
        public decimal PreviouslyAcceptedBaseQuantity { get; set; }
        public decimal ClosedRemainingQuantity { get; set; }
        public decimal RemainingBaseQuantity { get; set; }
        public decimal? OrderedProcurementQuantity { get; set; }
        public decimal? PreviouslyAcceptedProcurementQuantity { get; set; }
        public decimal? RemainingProcurementQuantity { get; set; }
        public string? ProcurementUnitName { get; set; }
        public decimal? ActualReceivedQuantity { get; set; }
        public decimal RejectedQuantity { get; set; }
        public string? RejectionReason { get; set; }
        public string? RejectionIssueType { get; set; }
    }

    public sealed class SavePurchaseOrderReceiptDraftRequest
    {
        public int BranchReceiptId { get; set; }
        public string RowVersion { get; set; } = string.Empty;
        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
        public List<SavePurchaseOrderReceiptDraftLineRequest> Lines { get; set; } = new();
    }

    public sealed class SavePurchaseOrderReceiptDraftLineRequest
    {
        public int PurchaseOrderLineId { get; set; }
        public decimal? ActualReceivedQuantity { get; set; }
        public decimal RejectedQuantity { get; set; }
        public string? RejectionReason { get; set; }
        public string? RejectionIssueType { get; set; }
    }

    public class BranchReceiptSupplierOptionDto
    {
        public int SupplierId { get; set; }
        public string SupplierCode { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public int? LeadTimeOverrideDays { get; set; }
        public string? DeliverySchedule { get; set; }
    }

    public class BranchReceiptOfferOptionDto
    {
        public int IngredientSupplierId { get; set; }
        public int SupplierId { get; set; }
        public int IngredientId { get; set; }
        public string IngredientName { get; set; } = string.Empty;
        public int PackageUnitId { get; set; }
        public string PackageUnitName { get; set; } = string.Empty;
        public decimal PackageQuantity { get; set; }
        public decimal PackagePrice { get; set; }
        public decimal MinimumOrderPackageCount { get; set; }
        public int LeadTimeDays { get; set; }
        public string PackageDisplay { get; set; } = string.Empty;
    }
}
