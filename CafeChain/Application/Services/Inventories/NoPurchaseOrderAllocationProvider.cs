using CafeChain.Application.Interfaces.Inventories;

namespace CafeChain.Application.Services.Inventories
{
    public sealed class NoPurchaseOrderAllocationProvider : IRestockPurchaseAllocationProvider
    {
        public Task<decimal> GetAllocatedBaseQuantityAsync(
            int restockRequestId,
            int? excludePurchaseOrderLineId = null) => Task.FromResult(0m);
    }
}
