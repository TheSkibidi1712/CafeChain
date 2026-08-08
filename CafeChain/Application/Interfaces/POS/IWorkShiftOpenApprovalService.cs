using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.POS;

public interface IWorkShiftOpenApprovalService
{
    Task<ServiceResult<WorkShiftOpenApprovalDto>> CreateAsync(
        int requesterStaffId, int storeId, CreateWorkShiftOpenApprovalRequestDto request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<WorkShiftOpenApprovalDto>> GetAsync(
        int actorStaffId, Guid publicId, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<WorkShiftOpenApprovalDto>>> GetPendingAsync(
        int actorStaffId, IReadOnlyCollection<int> allowedStoreIds,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<WorkShiftOpenApprovalDto>> DecideAsync(
        int decisionMakerStaffId, int storeId, Guid publicId,
        DecideWorkShiftOpenApprovalRequestDto request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<WorkShiftOpenApprovalDto>> CancelAsync(
        int requesterStaffId, int storeId, Guid publicId, string terminalId, string requestKey,
        CancellationToken cancellationToken = default);
    Task<int> ExpireDueAsync(CancellationToken cancellationToken = default);
}

public interface IWorkShiftOpenApprovalPublisher
{
    Task PublishAsync(WorkShiftOpenApprovalChangedDto notification,
        CancellationToken cancellationToken = default);
}
