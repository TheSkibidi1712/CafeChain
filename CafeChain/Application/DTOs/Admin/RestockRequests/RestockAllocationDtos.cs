namespace CafeChain.Application.DTOs.Admin.RestockRequests
{
    public sealed class RestockAllocationSummaryDto
    {
        public int RestockRequestId { get; set; }
        public decimal RequestedQuantity { get; set; }
        public decimal FulfilledQuantity { get; set; }
        public decimal TransferAllocatedQuantity { get; set; }
        public decimal PurchaseAllocatedQuantity { get; set; }
        public decimal ClosedRemainingQuantity { get; set; }
        public decimal RemainingUnallocatedQuantity { get; set; }
        public decimal RemainingToReceiveQuantity { get; set; }
    }

    public sealed class RestockAllocationValidationRequest
    {
        public int RestockRequestId { get; set; }
        public int DestinationStoreId { get; set; }
        public int? IngredientId { get; set; }
        public int? PreparedItemId { get; set; }
        public decimal AllocationQuantity { get; set; }
        public int? ExcludeInventoryTransferId { get; set; }
        public bool AllowOverallocationOverride { get; set; }
        public string? OverrideReason { get; set; }
        public int ActorStaffId { get; set; }
        public IReadOnlyCollection<string> ActorRoles { get; set; } = Array.Empty<string>();
        public string? RequestKey { get; set; }
    }
}
