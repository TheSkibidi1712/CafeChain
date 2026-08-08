using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.POS
{
    public interface IOtpApprovalService
    {
        Task<ServiceResult<OtpChallengeResponseDto>> GetCurrentOpenPosOtpStateAsync(
            int requestedByStaffId,
            int storeId);
        Task<ServiceResult<OtpChallengeResponseDto>> GetCurrentTerminalRegistrationOtpStateAsync(
            int requestedByStaffId,
            int storeId);

        Task<ServiceResult<OtpChallengeResponseDto>> RequestOtpAsync(
            OtpRequestDto request,
            int requestedByStaffId,
            int storeId);

        Task<ServiceResult<OtpChallengeResponseDto>> VerifyOtpAsync(OtpVerifyDto request);

        Task<ServiceResult<OtpChallengeResponseDto>> VerifyOtpAsync(
            OtpVerifyDto request,
            int requestedByStaffId,
            int storeId);

        Task<ServiceResult<OtpChallengeResponseDto>> ResendOtpAsync(OtpResendDto request);

        Task<ServiceResult<OtpChallengeResponseDto>> ResendOtpAsync(
            OtpResendDto request,
            int requestedByStaffId,
            int storeId);

        Task<ServiceResult<OtpChallengeResponseDto>> CancelTerminalRegistrationOtpAsync(
            OtpCancelDto request,
            int requestedByStaffId,
            int storeId);

        Task<ServiceResult<OtpChallengeResponseDto>> CancelOpenPosOtpAsync(
            OtpCancelDto request,
            int requestedByStaffId,
            int storeId);
    }
}
