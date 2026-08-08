using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.Production;

public interface IProductionSourceEligibilityService
{
    Task<ServiceResult<ProductionSourceEligibilityDto>> EvaluateAsync(
        ProductionSourceEligibilityRequest request);
}
