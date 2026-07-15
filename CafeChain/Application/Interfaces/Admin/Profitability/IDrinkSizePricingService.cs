using CafeChain.Application.DTOs.Admin.Profitability;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.Profitability
{
    public interface IDrinkSizePricingService
    {
        Task<ServiceResult<DrinkSizePriceUpdateResult>> UpdatePriceAsync(UpdateDrinkSizePriceRequest request, int storeIdForCostCheck, int actorStaffId, CancellationToken cancellationToken = default);
        Task<PosCatalogVersionDto> GetCatalogVersionAsync(CancellationToken cancellationToken = default);
    }
}
