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
                    x.OrderedBaseQuantity
                })
                .ToListAsync();
            var lineIds = rows.Select(x => x.PurchaseOrderLineId).ToArray();
            var postingRows = await _context.PurchaseOrderReceiptPostings.AsNoTracking()
                .Where(x => lineIds.Contains(x.PurchaseOrderLineId))
                .Select(x => new { x.PurchaseOrderLineId, x.AcceptedBaseQuantity, x.RejectedBaseQuantity })
                .ToListAsync();
            var disposedByLine = postingRows
                .GroupBy(x => x.PurchaseOrderLineId)
                .ToDictionary(
                    x => x.Key,
                    x => x.Sum(y => y.AcceptedBaseQuantity + y.RejectedBaseQuantity));
            return rows.GroupBy(x => x.IngredientId)
                .ToDictionary(
                    x => x.Key,
                    x => x.Sum(y => Math.Max(
                        0m,
                        y.OrderedBaseQuantity - disposedByLine.GetValueOrDefault(y.PurchaseOrderLineId))));
        }

        public async Task<decimal> GetAllocatedBaseQuantityAsync(int restockRequestId, int? excludePurchaseOrderLineId = null)
        {
            var quantities = await _context.PurchaseOrderLines.AsNoTracking()
                .Where(x => x.RestockRequestId == restockRequestId
                    && x.PurchaseOrder.Status != PurchaseOrderStatuses.Cancelled
                    && (!excludePurchaseOrderLineId.HasValue || x.PurchaseOrderLineId != excludePurchaseOrderLineId.Value))
                .Select(x => x.OrderedBaseQuantity)
                .ToListAsync();
            return quantities.Sum();
        }
    }
}
