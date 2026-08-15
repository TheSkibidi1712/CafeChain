using CafeChain.Application.DTOs.Admin.Replenishment;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.StoreInventories;

public interface IPreparedItemReplenishmentReadService
{
    Task<ServiceResult<PreparedItemReplenishmentDto>> GetAsync(
        int accountId,
        int storeId,
        int preparedItemId,
        int openRunLimit = 5);
}
