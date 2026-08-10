using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Operations;

public interface ITerminalRegistrationNotificationService
{
    Task<ServiceResult<OtpRevealResultDto>> RevealOperationalOtpAsync(
        int approverStaffId,
        int notificationId,
        IReadOnlyCollection<int>? allowedStoreIds = null);

    // Compatibility alias retained for one release cycle.
    Task<ServiceResult<OtpRevealResultDto>> RevealOtpAsync(
        int approverStaffId,
        int notificationId,
        IReadOnlyCollection<int>? allowedStoreIds = null);

    Task<ServiceResult<TerminalApprovalResultDto>> ConfirmAsync(
        int approverStaffId,
        int notificationId,
        ConfirmTerminalNotificationRequestDto request,
        IReadOnlyCollection<int>? allowedStoreIds = null);

    Task<ServiceResult<TerminalApprovalResultDto>> RejectAsync(
        int rejectorStaffId,
        int notificationId,
        RejectTerminalNotificationRequestDto request,
        IReadOnlyCollection<int>? allowedStoreIds = null);
}
