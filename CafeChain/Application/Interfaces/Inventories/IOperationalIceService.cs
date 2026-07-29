using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Results;
using CafeChain.Models.Orders;

namespace CafeChain.Application.Interfaces.Inventories;

public interface IOperationalIceService
{
    Task<ServiceResult<OperationalIcePolicySetupDto>> GetPolicySetupAsync(int storeId, CancellationToken cancellationToken = default);
    Task<ServiceResult> SavePolicyAsync(SaveIcePolicyRequest request, AdminActorContext actor, CancellationToken cancellationToken = default);
    Task<ServiceResult<OperationalShiftSummaryDto>> CreateShiftAsync(CreateOperationalShiftRequest request, AdminActorContext actor, CancellationToken cancellationToken = default);
    Task<ServiceResult<IceAllocationDto>> OpenAllocationAsync(OpenIceAllocationRequest request, AdminActorContext actor, CancellationToken cancellationToken = default);
    Task<ServiceResult> LinkWorkShiftAsync(LinkOperationalWorkShiftRequest request, AdminActorContext actor, CancellationToken cancellationToken = default);
    Task<ServiceResult<IceSupplementalIssueDto>> RequestSupplementalAsync(RequestSupplementalIceRequest request, AdminActorContext actor, CancellationToken cancellationToken = default);
    Task<ServiceResult<IceSupplementalIssueDto>> DecideSupplementalAsync(DecideSupplementalIceRequest request, AdminActorContext actor, CancellationToken cancellationToken = default);
    Task<ServiceResult<IceCarryOverDto>> ConfirmCarryOverAsync(ConfirmIceCarryOverRequest request, AdminActorContext actor, CancellationToken cancellationToken = default);
    Task<ServiceResult<IceCloseResultDto>> CloseAllocationAsync(CloseIceAllocationRequest request, AdminActorContext actor, CancellationToken cancellationToken = default);
    Task<ServiceResult<IceCloseResultDto>> ApproveVarianceAsync(ApproveIceVarianceRequest request, AdminActorContext actor, CancellationToken cancellationToken = default);
    Task<ServiceResult<IceCloseResultDto>> ReconcileVarianceAsync(ReconcileIceVarianceRequest request, AdminActorContext actor, CancellationToken cancellationToken = default);
    Task<ServiceResult> CancelAllocationAsync(CancelIceAllocationRequest request, AdminActorContext actor, CancellationToken cancellationToken = default);
}

public interface IOperationalIceReservationConsumptionService
{
    Task<ServiceResult> ConsumeForCommittedOrderAsync(
        Order committedOrder,
        IReadOnlyDictionary<int, decimal> ingredientRequirements,
        CancellationToken cancellationToken = default);
}
