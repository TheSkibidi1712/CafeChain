namespace CafeChain.Application.Interfaces.Inventories
{
    public interface IRestockPurchaseAllocationProvider
    {
        Task<decimal> GetAllocatedBaseQuantityAsync(int restockRequestId, int? excludePurchaseOrderLineId = null);
    }
}
