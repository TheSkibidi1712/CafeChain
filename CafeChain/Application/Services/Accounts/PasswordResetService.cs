using CafeChain.Application.DTOs.Accounts;
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

        public async Task<ServiceResult<SendOtpResponse>> SendOtpAsync(string email)
        {
            try
            {
                email = email?.Trim().ToLower();

                if (string.IsNullOrWhiteSpace(email))
                    return ServiceResult<SendOtpResponse>.Failure("Email không hợp lệ");

                var account = await _accountRepository.GetAccountByEmailAsync(email);
                if (account == null)
                    return ServiceResult<SendOtpResponse>.Failure("Email không tồn tại");

                // ===== 1. CHẶN SPAM (30s) =====
                var lastOtp = await _otpRepository.GetLatestOtpAsync(email);

                if (lastOtp != null && lastOtp.CreatedAt > DateTime.UtcNow.AddSeconds(-30))
                {
                    var wait = (int)Math.Ceiling(
                        (lastOtp.CreatedAt.AddSeconds(30) - DateTime.UtcNow).TotalSeconds
                    );

                    return ServiceResult<SendOtpResponse>.Failure(
                        $"Vui lòng đợi {wait} giây trước khi gửi lại OTP"
                    );
                }

                // ===== 2. INVALIDATE OTP CŨ =====
                await _otpRepository.InvalidateOldOtpsAsync(email);

                // ===== 3. GENERATE OTP =====
                var code = GenerateOtp();

                var otp = new PasswordResetOtp
                {
                    AccountId = account.AccountId,
                    Email = email,
                    CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
                    CreatedAt = DateTime.UtcNow,
                    ExpiredAt = DateTime.UtcNow.AddMinutes(5),
                    IsUsed = false,
                    FailedAttempts = 0 // 🔥 IMPORTANT
                };

                await _otpRepository.SaveOtpAsync(otp);

                // ===== 4. SEND MAIL =====
                var subject = "CafeChain | Mã OTP đặt lại mật khẩu";
                var body = _emailService.BuildOtpEmail(code);

                await _emailService.SendAsync(email, subject, body);

                // ===== 5. RETURN =====
                return ServiceResult<SendOtpResponse>.Success(
                    new SendOtpResponse
                    {
                        ExpireAt = otp.ExpiredAt
                    },
                    "OTP đã được gửi"
                );
            }
            catch (Exception ex)
            {
                return ServiceResult<SendOtpResponse>.Failure(
                    "Lỗi hệ thống khi gửi OTP",
                    new List<string> { ex.Message }
                );
            }
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
            if (!BCrypt.Net.BCrypt.Verify(code, otp.CodeHash))
            {
                await _otpRepository.IncreaseFailCountAsync(otp);

                var remaining = 5 - otp.FailedAttempts;

                if (remaining <= 0)
                    return ServiceResult.Failure("LOCKED");

                return ServiceResult.Failure($"OTP không đúng. Bạn còn {remaining} lần thử");
            }
            else
            {
                otp.FailedAttempts = 0;
            }   

            return ServiceResult.Success("OTP hợp lệ");
        }

        public async Task<ServiceResult> ResetPasswordAsync(string email, string code, string newPassword)
        {
            email = email.Trim().ToLower();
            code = code.Trim().ToUpper();
            var otp = await _otpRepository.GetValidOtpAsync(email);

            if (otp == null)
                return ServiceResult.Failure("OTP không hợp lệ hoặc đã hết hạn");

            // 🔥 VERIFY Ở SERVICE (ĐÚNG LAYER)
            if (!BCrypt.Net.BCrypt.Verify(code, otp.CodeHash))
                return ServiceResult.Failure("OTP không đúng");

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
