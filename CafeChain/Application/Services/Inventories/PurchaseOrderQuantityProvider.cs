using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories
{
    public sealed class PurchaseOrderQuantityProvider : IReorderIncomingQuantityProvider, IRestockPurchaseAllocationProvider
    {
        private readonly AppDbContext _context;

        public PurchaseOrderQuantityProvider(AppDbContext context) => _context = context;

        public async Task<IReadOnlyDictionary<int, decimal>> GetIncomingBaseQuantitiesAsync(
            int storeId,
            IReadOnlyCollection<int> ingredientIds)
        {
            var rows = await _context.PurchaseOrderLines.AsNoTracking()
                .Where(x => x.PurchaseOrder.StoreId == storeId
                    && ingredientIds.Contains(x.IngredientId)
                    && PurchaseOrderStatuses.IncomingValues.Contains(x.PurchaseOrder.Status))
                .Select(x => new
                {
                    x.PurchaseOrderLineId,
                    x.IngredientId,
                    x.OrderedBaseQuantity,
                    x.ClosedRemainingQuantity
                })
                .ToListAsync();
            var lineIds = rows.Select(x => x.PurchaseOrderLineId).ToArray();
            var postingRows = await _context.PurchaseOrderReceiptPostings.AsNoTracking()
                .Where(x => lineIds.Contains(x.PurchaseOrderLineId))
                .Select(x => new { x.PurchaseOrderLineId, x.AcceptedBaseQuantity })
                .ToListAsync();
            var acceptedByLine = postingRows
                .GroupBy(x => x.PurchaseOrderLineId)
                .ToDictionary(
                    x => x.Key,
                    x => x.Sum(y => y.AcceptedBaseQuantity));
            return rows.GroupBy(x => x.IngredientId)
                .ToDictionary(
                    x => x.Key,
                    x => x.Sum(y => Math.Max(
                        0m,
                        y.OrderedBaseQuantity
                        - acceptedByLine.GetValueOrDefault(y.PurchaseOrderLineId)
                        - y.ClosedRemainingQuantity)));
        }

        public async Task<decimal> GetAllocatedBaseQuantityAsync(int restockRequestId, int? excludePurchaseOrderLineId = null)
        {
            var purchaseOrderQuantities = await _context.PurchaseOrderLines.AsNoTracking()
                .Where(x => x.RestockRequestId == restockRequestId
                    && x.PurchaseOrder.Status != PurchaseOrderStatuses.Cancelled
                    && (!excludePurchaseOrderLineId.HasValue || x.PurchaseOrderLineId != excludePurchaseOrderLineId.Value))
                .Select(x => Math.Max(0m, x.OrderedBaseQuantity - x.ClosedRemainingQuantity))
                .ToListAsync();
            var purchaseAdviceQuantities = await _context.PurchaseAdviceLines.AsNoTracking()
                .Where(x => x.RestockRequestId == restockRequestId && x.IsActiveReservation)
                .Select(x => Math.Max(0m, x.RequestedPurchaseBaseQuantity - x.ClosedBaseQuantity))
                .ToListAsync();
            return purchaseOrderQuantities.Sum() + purchaseAdviceQuantities.Sum();
        }
    }
}
