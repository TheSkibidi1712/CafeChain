using CafeChain.Application.Interfaces.Inventories;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Replaced by the PurchaseOrder-backed provider in issue #178.
    /// Keeping the dependency explicit prevents RestockRequest quantities from being counted as incoming.
    /// </summary>
    public sealed class NoPurchaseOrderIncomingQuantityProvider : IReorderIncomingQuantityProvider
    {
        public Task<IReadOnlyDictionary<int, decimal>> GetIncomingBaseQuantitiesAsync(
            int storeId,
            IReadOnlyCollection<int> ingredientIds)
        {
            IReadOnlyDictionary<int, decimal> empty = new Dictionary<int, decimal>();
            return Task.FromResult(empty);
        }
    }
}
