using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.Production;

public interface IProductionRunQueryService
{
    Task<ServiceResult<ProductionRunListDto>> GetPageAsync(ProductionRunListQuery query, int accountId);
    Task<ServiceResult<ProductionRunDetailDto>> GetDetailAsync(int productionRunId, int accountId);
}
