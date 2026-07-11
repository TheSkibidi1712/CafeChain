using CafeChain.Application.DTOs.Inventories;

namespace CafeChain.Application.Interfaces.Inventories
{
    public interface IStoreInventoryWriteResolver
    {
        Task<StoreInventoryWriteResolution> ResolveAsync(StoreInventoryWriteRequest request);
    }
}
