using CafeChain.Application.DTOs.Inventories;

namespace CafeChain.Application.Interfaces.Inventories
{
    public interface IInventoryItemIdentityResolver
    {
        Task<InventoryItemIdentitySnapshot?> ResolveStoreInventoryAsync(int storeInventoryId);
    }
}
