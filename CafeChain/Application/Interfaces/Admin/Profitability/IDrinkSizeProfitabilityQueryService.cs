using CafeChain.Application.DTOs.Admin.Profitability;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.Profitability
{
    public interface IDrinkSizeProfitabilityQueryService
    {
        Task<ServiceResult<DrinkProfitabilityPreviewDto>> PreviewAsync(int storeId, int drinkId, DateTime asOfUtc, int actorStaffId, CancellationToken cancellationToken = default);
    }
}
