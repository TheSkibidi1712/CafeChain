using CafeChain.Application.Results;
using CafeChain.Models.Inventories.Costing;

namespace CafeChain.Application.Interfaces.Inventories
{
    /// <summary>
    /// Narrow FIFO cost-layer planner/consumer (#132).
    /// Does not commit; operates inside the caller's DbContext transaction.
    /// </summary>
    public interface IInventoryCostLayerConsumptionService
    {
        /// <summary>
        /// Load FIFO layers (RemainingQuantity &gt; 0), lock on SQL Server, plan full coverage.
        /// Does not mutate layers until <see cref="ApplyPlan"/> is called.
        /// </summary>
        Task<ServiceResult<CostLayerConsumptionPlan>> PlanConsumeAsync(
            int storeId,
            int? ingredientId,
            int? preparedItemId,
            decimal requiredBaseQuantity,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Apply RemainingQuantity decrements for a previously built plan (same tracked entities).
        /// </summary>
        void ApplyPlan(CostLayerConsumptionPlan plan);
    }

    public sealed class CostLayerConsumptionPlan
    {
        public int StoreId { get; init; }
        public int? IngredientId { get; init; }
        public int? PreparedItemId { get; init; }
        public decimal RequiredQuantity { get; init; }
        public decimal CoveredQuantity { get; init; }
        public decimal AvailableLayerQuantity { get; init; }
        public decimal TotalCost { get; init; }
        public decimal WeightedUnitCost { get; init; }
        public bool IsFullyCovered { get; init; }
        public IReadOnlyList<CostLayerAllocationSlice> Slices { get; init; } = Array.Empty<CostLayerAllocationSlice>();
    }

    public sealed class CostLayerAllocationSlice
    {
        public InventoryCostLayer Layer { get; init; } = null!;
        public int InventoryCostLayerId { get; init; }
        public decimal Quantity { get; init; }
        public decimal UnitCost { get; init; }
        public decimal TotalCost { get; init; }
    }

    public static class InventoryCostLayerConsumptionFailureCodes
    {
        public const string InvalidIdentity = "COST_LAYER_INVALID_IDENTITY";
        public const string InvalidQuantity = "COST_LAYER_INVALID_QUANTITY";
        public const string IncompleteEvidence = "PRODUCTION_COST_EVIDENCE_INCOMPLETE";
        public const string InvalidUnitCost = "COST_LAYER_INVALID_UNIT_COST";
    }
}
