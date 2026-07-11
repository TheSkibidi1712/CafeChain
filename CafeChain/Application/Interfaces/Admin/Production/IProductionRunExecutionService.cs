using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.Production
{
    /// <summary>Issue #120 — atomic stock apply for one CONFIRMED ProductionRun.</summary>
    public interface IProductionRunExecutionService
    {
        Task<ServiceResult<ProductionRunExecutionResultDto>> ExecuteAsync(
            int productionRunId,
            int staffId,
            int staffHomeStoreId);
    }
}
