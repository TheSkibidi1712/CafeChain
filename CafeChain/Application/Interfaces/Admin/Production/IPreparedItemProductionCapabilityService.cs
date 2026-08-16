using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.Production;

public interface IPreparedItemProductionCapabilityService
{
    Task<ServiceResult<PreparedItemProductionCapabilityPageDto>> GetPageAsync(
        int actorAccountId,
        int storeId,
        string? search,
        int page,
        int pageSize);

    Task<ServiceResult> SetGlobalProductionAsync(
        int actorAccountId,
        int actorStaffId,
        int preparedItemId,
        bool enabled,
        string? rowVersion);

    Task<ServiceResult> SetStoreProductionAsync(
        int actorAccountId,
        int actorStaffId,
        int storeId,
        int preparedItemId,
        bool enabled,
        string? rowVersion);
}
