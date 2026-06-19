using CafeChain.Application.DTOs.Accounts;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Accounts
{
    public interface IPasswordResetService
    {
        Task<ServiceResult<SendOtpResponse>> SendOtpAsync(string email);
        Task<ServiceResult> VerifyOtpAsync(string email, string code);
        Task<ServiceResult> ResetPasswordAsync(string email, string code, string newPassword);
    }
}
