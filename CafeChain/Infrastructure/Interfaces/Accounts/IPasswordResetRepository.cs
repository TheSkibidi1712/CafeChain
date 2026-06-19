using CafeChain.Models.Customers;

namespace CafeChain.Infrastrusture.Interfaces.Accounts
{
    public interface IPasswordResetRepository
    {
        Task SaveOtpAsync(PasswordResetOtp otp);
        Task<PasswordResetOtp> GetValidOtpAsync(string email);
        Task MarkOtpUsedAsync(PasswordResetOtp otp);
        Task UpdatePasswordAsync(string email, string newPasswordHash);
        Task<PasswordResetOtp?> GetLatestOtpAsync(string email);
        Task InvalidateOldOtpsAsync(string email);
        Task IncreaseFailCountAsync(PasswordResetOtp otp);
    }
}
