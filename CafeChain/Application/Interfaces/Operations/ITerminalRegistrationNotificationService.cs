using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Operations;

public interface ITerminalRegistrationNotificationService
{
    Task<ServiceResult<OtpRevealResultDto>> RevealOtpAsync(
        int approverStaffId,
        int notificationId,
        IReadOnlyCollection<int>? allowedStoreIds = null);

    Task<ServiceResult> ConfirmAsync(
        int approverStaffId,
        int notificationId,
        ConfirmTerminalNotificationRequestDto request,
        IReadOnlyCollection<int>? allowedStoreIds = null);
}
