using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.POS
{
    public interface IOtpApprovalService
    {
        Task<ServiceResult<OtpChallengeResponseDto>> RequestOtpAsync(
            OtpRequestDto request,
            int requestedByStaffId,
            int storeId);

        Task<ServiceResult<OtpChallengeResponseDto>> VerifyOtpAsync(OtpVerifyDto request);

        Task<ServiceResult<OtpChallengeResponseDto>> ResendOtpAsync(OtpResendDto request);
    }
}
