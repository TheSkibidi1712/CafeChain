using CafeChain.Application.DTOs.Admin.Profitability;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.Profitability
{
    public interface IDrinkSizeToppingPolicyService
    {
        Task<IReadOnlyList<DrinkSizeToppingPolicyDto>> GetActiveAsync(int drinkSizeId, CancellationToken cancellationToken = default);
        Task<ServiceResult<DrinkSizeToppingPolicyDto>> UpsertAsync(UpsertDrinkSizeToppingPolicyRequest request, int actorStaffId, CancellationToken cancellationToken = default);
    }
}
