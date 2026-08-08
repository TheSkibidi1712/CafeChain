using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.Production;

public interface IProductionRunOperationsService
{
    Task<ServiceResult<ProductionRunOperationResultDto>> ReleaseAsync(int productionRunId, int actorStaffId);
    Task<ServiceResult<ProductionRunOperationResultDto>> StartAsync(int productionRunId, int actorStaffId);
    Task<ServiceResult<ProductionRunOperationResultDto>> RecordActualAsync(
        RecordProductionActualRequest request,
        int actorStaffId);
    Task<ServiceResult<ProductionRunOperationResultDto>> ApproveVarianceAsync(
        int productionRunId,
        int actorStaffId,
        string? reason);
    Task<ServiceResult<ProductionRunOperationResultDto>> CancelAsync(
        int productionRunId,
        int actorStaffId,
        string reason);
}
