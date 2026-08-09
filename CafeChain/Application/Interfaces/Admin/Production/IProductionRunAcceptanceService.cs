using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.Production;

public interface IProductionRunAcceptanceService
{
    Task<ServiceResult<ProductionRunExecutionResultDto>> AcceptAsync(
        int productionRunId,
        int actorStaffId);
}
