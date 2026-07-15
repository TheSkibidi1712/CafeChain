using CafeChain.Application.DTOs.Admin.StoreMenu;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.StoreMenu
{
    public interface IStoreMenuWorkspaceService
    {
        Task<ServiceResult<IReadOnlyList<StoreMenuWorkspaceRowDto>>> GetRowsAsync(
            int storeId,
            int actorStaffId,
            DateTime asOfUtc,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<StoreMenuWorkspaceRowDto>> UpdateLifecycleAsync(
            UpdateStoreMenuLifecycleRequest request,
            int actorStaffId,
            CancellationToken cancellationToken = default);
    }
}
