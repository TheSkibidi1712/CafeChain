using CafeChain.Application.DTOs.Admin.StoreMenu;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.StoreMenu;

public interface IStoreMenuProvisioningService
{
    Task<ServiceResult<StoreMenuProvisioningResultDto>> ProvisionMissingAsync(
        int storeId,
        int actorAccountId,
        int actorStaffId,
        CancellationToken cancellationToken = default);
}
