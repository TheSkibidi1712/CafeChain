using CafeChain.Application.Results;
using CafeChain.Models.Stores;

namespace CafeChain.Application.Interfaces.Admin.StoreInventories;

public interface IPreparedItemInventoryBootstrapService
{
    Task<ServiceResult<StoreInventory>> EnsureAsync(
        int storeId,
        int preparedItemId,
        int actorAccountId,
        string evidenceReference);
}
