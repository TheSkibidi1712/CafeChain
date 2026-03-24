using CafeChain.Application.Interfaces.Accounts;
using CafeChain.Application.Results;
using CafeChain.Infrastrusture.Interfaces.Accounts;
using CafeChain.Models.Customers;
using System.Security.Cryptography;

namespace CafeChain.Application.Services.Accounts
{
    public class PasswordResetService : IPasswordResetService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IPasswordResetRepository _otpRepository;
        private readonly IEmailService _emailService;

        public PasswordResetService(
            IAccountRepository accountRepository,
            IPasswordResetRepository otpRepository,
            IEmailService emailService)
        {
            _accountRepository = accountRepository;
            _otpRepository = otpRepository;
            _emailService = emailService;
        }

        public async Task<ServiceResult> SendOtpAsync(string email)
        {
            email = email.Trim().ToLower();

            var account = await _accountRepository.GetAccountByEmailAsync(email);
            if (account == null)
                return ServiceResult.Failure("Email không tồn tại");

            // 🔥 1. CHẶN SPAM (30s)
            var lastOtp = await _otpRepository.GetLatestOtpAsync(email);
            if (lastOtp != null && lastOtp.CreatedAt > DateTime.UtcNow.AddSeconds(-30))
            {
                return ServiceResult.Failure("Vui lòng đợi 30 giây trước khi gửi lại OTP");
            }

            // 🔥 2. INVALIDATE OTP CŨ
            await _otpRepository.InvalidateOldOtpsAsync(email);

            var code = GenerateOtp();

            var otp = new PasswordResetOtp
            {
                Email = email,
                Code = code,
                CreatedAt = DateTime.UtcNow,
                ExpiredAt = DateTime.UtcNow.AddMinutes(5),
                IsUsed = false
            };

            await _otpRepository.SaveOtpAsync(otp);

            var subject = "CafeChain | Mã OTP đặt lại mật khẩu";
            var body = _emailService.BuildOtpEmail(code);

            await _emailService.SendAsync(email, subject, body);

            return ServiceResult.Success("OTP đã được gửi");
        }

        public async Task<ServiceResult> VerifyOtpAsync(string email, string code)
        {
            email = email.Trim().ToLower();
            code = code.Trim().ToUpper();

            // 🔥 LUÔN LẤY OTP MỚI NHẤT
            var otp = await _otpRepository.GetLatestOtpAsync(email);

            if (otp == null)
            {
                return ServiceResult.Failure("Không tìm thấy OTP");
            }

            // 🔥 CHECK EXPIRE
            if (otp.ExpiredAt < DateTime.UtcNow)
            {
                return ServiceResult.Failure("OTP đã hết hạn");
            }

            // 🔥 CHECK USED
            if (otp.IsUsed)
            {
                return ServiceResult.Failure("OTP đã được sử dụng");
            }

            // 🔥 CHECK FAIL LIMIT
            if (otp.FailedAttempts >= 5)
            {
                return ServiceResult.Failure("LOCKED");
            }

            // 🔥 SO SÁNH CODE
            if (otp.Code != code)
            {
                await _otpRepository.IncreaseFailCountAsync(otp);

                var remaining = 5 - otp.FailedAttempts;

                if (remaining <= 0)
                    return ServiceResult.Failure("LOCKED");

                return ServiceResult.Failure($"OTP không đúng. Bạn còn {remaining} lần thử");
            }

            return ServiceResult.Success("OTP hợp lệ");
        }

        public async Task<ServiceResult> ResetPasswordAsync(string email, string code, string newPassword)
        {
            email = email.Trim().ToLower();
            code = code.Trim().ToUpper();
            var otp = await _otpRepository.GetValidOtpAsync(email, code);

            if (otp == null)
                return ServiceResult.Failure("OTP không hợp lệ hoặc đã hết hạn");

            var hash = BCrypt.Net.BCrypt.HashPassword(newPassword);

            await _otpRepository.UpdatePasswordAsync(email, hash);
            await _otpRepository.MarkOtpUsedAsync(otp);

            return ServiceResult.Success("Đổi mật khẩu thành công");
        }

        // ===== PRIVATE METHODS =====

        private string GenerateOtp()
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var bytes = new byte[6];

            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);

            return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
        }
    }
}
