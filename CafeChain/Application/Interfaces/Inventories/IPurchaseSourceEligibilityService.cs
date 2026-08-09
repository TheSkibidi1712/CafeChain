using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories;

public interface IPurchaseSourceEligibilityService
{
    Task<ServiceResult<PurchaseSourceEligibilityDto>> EvaluateAsync(
        PurchaseSourceEligibilityRequest request);
}
