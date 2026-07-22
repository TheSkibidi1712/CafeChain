using CafeChain.Application.DTOs.Accounts;
using CafeChain.Application.Results;
using System.Threading.Tasks;

namespace CafeChain.Application.Interfaces.Accounts
{
    public interface IAccountService
    {
        Task<ServiceResult> RegisterCustomerAsync(RegisterDto dto);
        Task<ServiceResult<LoginResponseDto>> LoginAsync(LoginDto dto);
        Task<(bool IsLocked, int RemainingMinutes)> CheckLockAsync(string email);
        Task<ServiceResult> ChangeRequiredPasswordAsync(int accountId, string oldPassword, string newPassword);
        Task<ServiceResult> ChangePasswordAsync(int accountId, string oldPassword, string newPassword);
    }
}
