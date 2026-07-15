using CafeChain.Application.DTOs.Admin.StoreMenu;

namespace CafeChain.Application.Interfaces.Admin.StoreMenu
{
    public interface IStoreMenuAvailabilityEvaluator
    {
        Task<StoreMenuAvailabilityDto> EvaluateAsync(
            int storeId,
            int drinkSizeId,
            DateTime asOfUtc,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyDictionary<int, StoreMenuAvailabilityDto>> EvaluateStoreAsync(
            int storeId,
            DateTime asOfUtc,
            CancellationToken cancellationToken = default);
    }
}
