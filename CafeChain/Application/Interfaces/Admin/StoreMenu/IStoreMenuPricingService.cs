using CafeChain.Application.DTOs.Admin.StoreMenu;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.StoreMenu
{
    public interface IStoreMenuPricingService
    {
        Task<ServiceResult<StoreMenuPriceDto>> GetAsync(
            int storeMenuItemId,
            int actorStaffId,
            CancellationToken cancellationToken = default);

        Task<ServiceResult<StoreMenuPriceDto>> UpdateOverrideAsync(
            UpdateStoreMenuPriceOverrideRequest request,
            int actorStaffId,
            CancellationToken cancellationToken = default);
    }
}
