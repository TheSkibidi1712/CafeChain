namespace CafeChain.Application.DTOs.Admin.RestockRequests
{
    public sealed class CreateProcurementDemandRequest
    {
        public int StoreId { get; set; }
        public int IngredientId { get; set; }
        public decimal RequestedProcurementQuantity { get; set; }
        public int ProcurementUnitId { get; set; }
        public DateTime? NeedByDate { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public string? SourceReferenceId { get; set; }
        public decimal? TargetStockProcurementQuantity { get; set; }
        public string? ForecastEvidence { get; set; }
        public string? Priority { get; set; }
        public string? Note { get; set; }
    }

    public sealed class SourcingDecisionRequest
    {
        public int RestockRequestId { get; set; }
        public string DecisionType { get; set; } = string.Empty;
        public decimal ProcurementQuantity { get; set; }
        public int ProcurementUnitId { get; set; }
        public int? SourceDocumentId { get; set; }
        public int? SourceDocumentLineId { get; set; }
        public Guid? RequestKey { get; set; }
        public string? Reason { get; set; }
    }

    public sealed class ActiveRestockRequestDto
    {
        public int RestockRequestId { get; set; }
        public string ReferenceCode { get; set; } = string.Empty;
        public int StoreId { get; set; }
        public int IngredientId { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal RequestedProcurementQuantity { get; set; }
        public decimal AllocatedProcurementQuantity { get; set; }
        public decimal RemainingUnallocatedProcurementQuantity { get; set; }
        public int ProcurementUnitId { get; set; }
        public string ProcurementUnitName { get; set; } = string.Empty;
        public DateTime? NeedByDate { get; set; }
        public string RowVersion { get; set; } = string.Empty;
    }

    public sealed class AddRestockDemandAdjustmentRequest
    {
        public int RestockRequestId { get; set; }
        public decimal AdjustmentProcurementQuantity { get; set; }
        public int ProcurementUnitId { get; set; }
        public DateTime? NeedByDate { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string RowVersion { get; set; } = string.Empty;
        public string RequestKey { get; set; } = string.Empty;
    }

    public sealed class RestockDemandAdjustmentResultDto
    {
        public int RestockRequestId { get; set; }
        public decimal QuantityBefore { get; set; }
        public decimal AdjustmentQuantity { get; set; }
        public decimal QuantityAfter { get; set; }
        public decimal RemainingUnallocatedProcurementQuantity { get; set; }
        public string ProcurementUnitName { get; set; } = string.Empty;
        public string RowVersion { get; set; } = string.Empty;
        public bool WasReplay { get; set; }
    }

    public sealed class SourcingAllocationDto
    {
        public int RestockSourcingAllocationId { get; set; }
        public int RestockRequestId { get; set; }
        public string DecisionType { get; set; } = string.Empty;
        public decimal ProcurementQuantity { get; set; }
        public int ProcurementUnitId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? PurchaseAdviceLineId { get; set; }
        public int? PurchaseOrderLineId { get; set; }
        public int? InventoryTransferId { get; set; }
        public int? SourceDocumentId { get; set; }
        public int? SourceDocumentLineId { get; set; }
        public int? ProductionRunId { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class RestockRequestListItemDto
    {
        public int RestockRequestId { get; set; }
        public string ReferenceCode { get; set; } = string.Empty;
        public int? StockAlertId { get; set; }
        public int StoreId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemTypeLabel { get; set; } = string.Empty;
        public string BaseUnitName { get; set; } = string.Empty;
        public decimal RequestedQuantity { get; set; }
        public decimal? SuggestedQuantity { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? NeedByDate { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public string? SourceReferenceId { get; set; }
        public int? CreatedForStoreId { get; set; }
        public string SourcingStatus { get; set; } = string.Empty;
        public string? SourcingDecision { get; set; }
        public decimal? RequestedProcurementQuantity { get; set; }
        public int? ProcurementUnitId { get; set; }
        public string? ProcurementUnitName { get; set; }
    }

    public class RestockRequestDetailDto : RestockRequestListItemDto
    {
        public int? IngredientId { get; set; }
        public int? RecipeId { get; set; }
        /// <summary>Issue #122 — stable BTP identity when present.</summary>
        public int? PreparedItemId { get; set; }
        public int CreatedByStaffId { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? StoreName { get; set; }
        public string? AlertType { get; set; }
        public string? AlertStatus { get; set; }
        public decimal? AlertCurrentQtySnapshot { get; set; }
        public decimal? AlertThresholdSnapshot { get; set; }
        public int? SuggestionAnalysisWindowDays { get; set; }
        public decimal? SuggestionAvailableSnapshot { get; set; }
        public decimal? SuggestionMinLevelSnapshot { get; set; }
        public decimal? SuggestionAverageDailyUsageSnapshot { get; set; }
        public int? SuggestionLeadTimeDaysSnapshot { get; set; }
        public decimal? SuggestionIncomingQuantitySnapshot { get; set; }
        public string? SuggestionReason { get; set; }
        public string RowVersion { get; set; } = string.Empty;
        public List<SourcingAllocationDto> SourcingAllocations { get; set; } = new();
    }

    public class RestockRequestListResultDto
    {
        public int StoreId { get; set; }
        public string? StatusFilter { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public List<RestockRequestListItemDto> Items { get; set; } = new();
    }

    public class CreateRestockRequestResultDto
    {
        public int RestockRequestId { get; set; }
        public string ReferenceCode { get; set; } = string.Empty;
        public bool AlreadyExisted { get; set; }
        public bool NotifiedAccountantWarehouse { get; set; }
        public int RecipientCount { get; set; }
        public ActiveRestockRequestDto? ExistingActiveRequest { get; set; }
    }
}
