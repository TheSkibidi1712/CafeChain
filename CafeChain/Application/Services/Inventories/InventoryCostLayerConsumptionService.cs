using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Inventories.Costing;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Deterministic FIFO consumption for Ingredient or PreparedItem cost layers.
    /// Lock order: InventoryCostLayerId ASC after load (CreatedAt ASC, Id ASC selection).
    /// </summary>
    public sealed class InventoryCostLayerConsumptionService : IInventoryCostLayerConsumptionService
    {
        private readonly AppDbContext _context;

        public InventoryCostLayerConsumptionService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ServiceResult<CostLayerConsumptionPlan>> PlanConsumeAsync(
            int storeId,
            int? ingredientId,
            int? preparedItemId,
            decimal requiredBaseQuantity,
            bool requireFullCoverage = true,
            CancellationToken cancellationToken = default)
        {
            var hasIng = ingredientId is > 0;
            var hasPi = preparedItemId is > 0;
            if (hasIng == hasPi)
            {
                return ServiceResult<CostLayerConsumptionPlan>.Failure(
                    "Cost layer identity phải có đúng một IngredientId hoặc PreparedItemId.",
                    errorCode: InventoryCostLayerConsumptionFailureCodes.InvalidIdentity);
            }

            if (requiredBaseQuantity <= 0)
            {
                return ServiceResult<CostLayerConsumptionPlan>.Failure(
                    "Số lượng consume phải > 0.",
                    errorCode: InventoryCostLayerConsumptionFailureCodes.InvalidQuantity);
            }

            var layers = await LoadLayersForUpdateAsync(
                storeId,
                hasIng ? ingredientId : null,
                hasPi ? preparedItemId : null,
                cancellationToken);

            layers = layers
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.InventoryCostLayerId)
                .ToList();

            var available = layers.Where(x => x.UnitCost > 0).Sum(x => x.RemainingQuantity);
            var remaining = requiredBaseQuantity;
            var slices = new List<CostLayerAllocationSlice>();
            decimal totalCost = 0m;

            foreach (var layer in layers)
            {
                if (remaining <= 0)
                    break;

                // Layers without valid unit cost do not cover evidence (no zero fake).
                if (layer.UnitCost <= 0)
                    continue;

                if (layer.RemainingQuantity <= 0)
                    continue;

                var take = Math.Min(remaining, layer.RemainingQuantity);
                if (take <= 0)
                    continue;

                var sliceCost = take * layer.UnitCost;
                slices.Add(new CostLayerAllocationSlice
                {
                    Layer = layer,
                    InventoryCostLayerId = layer.InventoryCostLayerId,
                    Quantity = take,
                    UnitCost = layer.UnitCost,
                    TotalCost = sliceCost
                });
                totalCost += sliceCost;
                remaining -= take;
            }

            var covered = requiredBaseQuantity - remaining;
            var fully = remaining <= 0 && covered == requiredBaseQuantity && slices.Count > 0
                        && covered > 0;

            // Edge: required > 0, no layers → not full
            if (requiredBaseQuantity > 0 && covered <= 0)
                fully = false;

            if (requireFullCoverage && !fully)
            {
                return ServiceResult<CostLayerConsumptionPlan>.Failure(
                    $"Không đủ bằng chứng giá vốn FIFO. Cần {requiredBaseQuantity}, layer còn {available}.",
                    errorCode: InventoryCostLayerConsumptionFailureCodes.IncompleteEvidence);
            }

            var weighted = covered > 0 ? totalCost / covered : 0m;

            return ServiceResult<CostLayerConsumptionPlan>.Success(new CostLayerConsumptionPlan
            {
                StoreId = storeId,
                IngredientId = hasIng ? ingredientId : null,
                PreparedItemId = hasPi ? preparedItemId : null,
                RequiredQuantity = requiredBaseQuantity,
                CoveredQuantity = covered,
                AvailableLayerQuantity = available,
                TotalCost = totalCost,
                WeightedUnitCost = weighted,
                IsFullyCovered = fully,
                Slices = slices
            });
        }

        public void ApplyPlan(CostLayerConsumptionPlan plan)
        {
            if (plan == null)
                throw new InvalidOperationException("Cannot apply null cost-layer plan.");

            foreach (var slice in plan.Slices)
            {
                if (slice.Quantity <= 0)
                    continue;

                var layer = slice.Layer;
                if (layer.RemainingQuantity < slice.Quantity)
                {
                    throw new InvalidOperationException(
                        $"Layer #{layer.InventoryCostLayerId} remaining changed after plan.");
                }

                layer.RemainingQuantity -= slice.Quantity;
            }
        }

        private async Task<List<InventoryCostLayer>> LoadLayersForUpdateAsync(
            int storeId,
            int? ingredientId,
            int? preparedItemId,
            CancellationToken cancellationToken)
        {
            if (_context.Database.IsSqlServer())
            {
                List<InventoryCostLayer> locked;
                if (ingredientId.HasValue)
                {
                    locked = await _context.InventoryCostLayers
                        .FromSqlInterpolated(
                            $@"SELECT * FROM InventoryCostLayers WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
                               WHERE StoreId = {storeId}
                                 AND IngredientId = {ingredientId.Value}
                                 AND PreparedItemId IS NULL
                                 AND RemainingQuantity > 0")
                        .ToListAsync(cancellationToken);
                }
                else
                {
                    locked = await _context.InventoryCostLayers
                        .FromSqlInterpolated(
                            $@"SELECT * FROM InventoryCostLayers WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
                               WHERE StoreId = {storeId}
                                 AND PreparedItemId = {preparedItemId!.Value}
                                 AND IngredientId IS NULL
                                 AND RemainingQuantity > 0")
                        .ToListAsync(cancellationToken);
                }

                return locked
                    .OrderBy(x => x.CreatedAt)
                    .ThenBy(x => x.InventoryCostLayerId)
                    .ToList();
            }

            IQueryable<InventoryCostLayer> query = _context.InventoryCostLayers
                .Where(x => x.StoreId == storeId && x.RemainingQuantity > 0);

            if (ingredientId.HasValue)
            {
                query = query.Where(x => x.IngredientId == ingredientId.Value && x.PreparedItemId == null);
            }
            else
            {
                query = query.Where(x => x.PreparedItemId == preparedItemId && x.IngredientId == null);
            }

            return await query
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.InventoryCostLayerId)
                .ToListAsync(cancellationToken);
        }
    }
}
