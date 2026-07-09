using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Accounts;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Operations;
using CafeChain.Models.Staffs;
using System.Security.Cryptography;

namespace CafeChain.Application.Services.POS
{
    public class OtpApprovalService : IOtpApprovalService
    {
        private readonly IOtpChallengeRepository _repository;
        private readonly IEmailService _emailService;
        private readonly ILogger<OtpApprovalService> _logger;

        public OtpApprovalService(
            IOtpChallengeRepository repository,
            IEmailService emailService,
            ILogger<OtpApprovalService> logger)
        {
            _repository = repository;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<ServiceResult<OtpChallengeResponseDto>> RequestOtpAsync(
            OtpRequestDto request,
            int requestedByStaffId,
            int storeId)
        {
            if (request == null)
                return ServiceResult<OtpChallengeResponseDto>.Failure("Thiếu dữ liệu yêu cầu OTP.");

            var validation = ValidateFoundationRequest(request);
            if (validation != null)
                return ServiceResult<OtpChallengeResponseDto>.Failure(validation);

            var requester = await _repository.GetRequestingStaffAsync(requestedByStaffId, storeId);
            if (requester == null)
                return ServiceResult<OtpChallengeResponseDto>.Failure("Không tìm thấy nhân viên yêu cầu hợp lệ tại cửa hàng này.");

            var nowUtc = DateTime.UtcNow;
            var approver = await _repository.GetOtpApproverAsync(storeId, nowUtc);
            if (approver == null || string.IsNullOrWhiteSpace(approver.Account?.Email))
            {
                return ServiceResult<OtpChallengeResponseDto>.Failure(
                    "Không tìm thấy Ca trưởng/Cửa hàng trưởng đang hoạt động có email tại cửa hàng này.");
            }

            var store = await _repository.GetStoreAsync(storeId);
            if (store == null)
                return ServiceResult<OtpChallengeResponseDto>.Failure("Không tìm thấy cửa hàng cho yêu cầu OTP.");

            var otpCode = GenerateOtpCode();
            var challenge = new OtpChallenge
            {
                PublicId = Guid.NewGuid(),
                StoreId = storeId,
                WorkShiftId = request.WorkShiftId,
                RequestedByStaffId = requestedByStaffId,
                ApproverStaffId = approver.StaffId,
                ActionType = OtpConstants.ActionTypes.CashDifference,
                TargetType = OtpConstants.TargetTypes.Shifts,
                TargetId = request.TargetId,
                Reason = request.Reason.Trim(),
                OtpHash = BCrypt.Net.BCrypt.HashPassword(otpCode),
                ExpiresAt = nowUtc.AddMinutes(OtpConstants.TtlMinutes),
                LastSentAt = nowUtc,
                CreatedAt = nowUtc,
                Status = OtpConstants.Statuses.Pending,
                OldValueJson = request.OldValueJson,
                NewValueJson = request.NewValueJson
            };

            // Save challenge TRƯỚC khi gửi email — tránh email thành công mà challenge chưa tồn tại
            await _repository.AddAsync(challenge);

            var subject = $"[Xác nhận ca trưởng] Lệch két cần xác nhận - {store.Name}";
            var body = _emailService.BuildOperationalOtpEmail(
                otpCode,
                store.Name,
                BuildTargetLabel(challenge),
                requester.FullName,
                "Lệch két cuối ca",
                challenge.Reason,
                nowUtc,
                OtpConstants.TtlMinutes);

            try
            {
                await _emailService.SendAsync(approver.Account.Email.Trim(), subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "OTP_EMAIL_SEND_FAILED | StoreId={StoreId} | RequestedByStaffId={RequestedByStaffId} | ApproverStaffId={ApproverStaffId}",
                    storeId,
                    requestedByStaffId,
                    approver.StaffId);

                // Email fail → mark challenge Cancelled (giữ audit, không xóa)
                challenge.Status = OtpConstants.Statuses.Cancelled;
                challenge.CancelledAt = DateTime.UtcNow;
                await _repository.SaveChangesAsync();

                return ServiceResult<OtpChallengeResponseDto>.Failure(
                    "Không gửi được OTP ca trưởng. Vui lòng kiểm tra cấu hình email.");
            }

            return ServiceResult<OtpChallengeResponseDto>.Success(
                MapResponse(challenge, nowUtc),
                "OTP đã được gửi đến email ca trưởng.");
        }

        public async Task<ServiceResult<OtpChallengeResponseDto>> VerifyOtpAsync(OtpVerifyDto request)
        {
            if (request == null || request.OtpChallengePublicId == Guid.Empty)
                return ServiceResult<OtpChallengeResponseDto>.Failure("Thiếu mã yêu cầu OTP.");

            var challenge = await _repository.GetByPublicIdAsync(request.OtpChallengePublicId);
            if (challenge == null)
                return ServiceResult<OtpChallengeResponseDto>.Failure("Không tìm thấy yêu cầu OTP.");

            var nowUtc = DateTime.UtcNow;
            var statusFailure = EnsurePendingChallenge(challenge, nowUtc);
            if (statusFailure != null)
            {
                await _repository.SaveChangesAsync();
                return Failure(statusFailure, challenge, nowUtc);
            }

            var code = request.OtpCode?.Trim() ?? string.Empty;
            var isValidFormat = code.Length == OtpConstants.CodeLength && code.All(char.IsDigit);
            var isValidOtp = isValidFormat && BCrypt.Net.BCrypt.Verify(code, challenge.OtpHash);

            if (!isValidOtp)
            {
                challenge.FailedAttempts++;
                if (challenge.FailedAttempts >= OtpConstants.MaxFailedAttempts)
                {
                    challenge.Status = OtpConstants.Statuses.Locked;
                    challenge.LockedAt = nowUtc;
                    await _repository.SaveChangesAsync();
                    return Failure("Yêu cầu OTP đã bị khóa do nhập sai quá số lần cho phép.", challenge, nowUtc);
                }

                await _repository.SaveChangesAsync();
                return Failure(
                    $"OTP không đúng. Bạn còn {OtpConstants.MaxFailedAttempts - challenge.FailedAttempts} lần thử.",
                    challenge,
                    nowUtc);
            }

            challenge.Status = OtpConstants.Statuses.Approved;
            challenge.ApprovedAt = nowUtc;
            // Giữ FailedAttempts nguyên để audit — không reset về 0

            await _repository.SaveChangesAsync();

            return ServiceResult<OtpChallengeResponseDto>.Success(
                MapResponse(challenge, nowUtc),
                "Xác nhận OTP thành công.");
        }

        public async Task<ServiceResult<OtpChallengeResponseDto>> ResendOtpAsync(OtpResendDto request)
        {
            if (request == null || request.OtpChallengePublicId == Guid.Empty)
                return ServiceResult<OtpChallengeResponseDto>.Failure("Thiếu mã yêu cầu OTP.");

            var challenge = await _repository.GetByPublicIdAsync(request.OtpChallengePublicId);
            if (challenge == null)
                return ServiceResult<OtpChallengeResponseDto>.Failure("Không tìm thấy yêu cầu OTP.");

            var nowUtc = DateTime.UtcNow;
            if (challenge.Status != OtpConstants.Statuses.Pending &&
                challenge.Status != OtpConstants.Statuses.Expired)
            {
                return Failure("Yêu cầu OTP hiện tại không thể gửi lại.", challenge, nowUtc);
            }

            if (challenge.ResendCount >= OtpConstants.MaxResendCount)
                return Failure("Yêu cầu OTP đã vượt quá số lần gửi lại cho phép.", challenge, nowUtc);

            var nextAllowed = challenge.LastSentAt.AddSeconds(OtpConstants.ResendCooldownSeconds);
            if (nextAllowed > nowUtc)
            {
                var waitSeconds = (int)Math.Ceiling((nextAllowed - nowUtc).TotalSeconds);
                return Failure($"Vui lòng đợi {waitSeconds} giây trước khi gửi lại OTP.", challenge, nowUtc);
            }

            if (challenge.Store == null || challenge.ApproverStaff?.Account == null)
                return Failure("Yêu cầu OTP thiếu dữ liệu cửa hàng hoặc người duyệt.", challenge, nowUtc);

            var otpCode = GenerateOtpCode();
            var subject = $"[Xác nhận ca trưởng] Lệch két cần xác nhận - {challenge.Store.Name}";
            var body = _emailService.BuildOperationalOtpEmail(
                otpCode,
                challenge.Store.Name,
                BuildTargetLabel(challenge),
                challenge.RequestedByStaff?.FullName ?? $"Staff #{challenge.RequestedByStaffId}",
                "Lệch két cuối ca",
                challenge.Reason,
                nowUtc,
                OtpConstants.TtlMinutes);

            try
            {
                await _emailService.SendAsync(challenge.ApproverStaff.Account.Email.Trim(), subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "OTP_EMAIL_RESEND_FAILED | OtpChallengePublicId={OtpChallengePublicId} | StoreId={StoreId} | ApproverStaffId={ApproverStaffId}",
                    challenge.PublicId,
                    challenge.StoreId,
                    challenge.ApproverStaffId);

                return Failure("Không gửi lại được OTP ca trưởng. Vui lòng kiểm tra cấu hình email.", challenge, nowUtc);
            }

            challenge.OtpHash = BCrypt.Net.BCrypt.HashPassword(otpCode);
            challenge.ExpiresAt = nowUtc.AddMinutes(OtpConstants.TtlMinutes);
            challenge.LastSentAt = nowUtc;
            challenge.ResendCount++;
            challenge.FailedAttempts = 0;
            challenge.Status = OtpConstants.Statuses.Pending;
            challenge.ApprovedAt = null;
            challenge.LockedAt = null;
            challenge.CancelledAt = null;

            await _repository.SaveChangesAsync();

            return ServiceResult<OtpChallengeResponseDto>.Success(
                MapResponse(challenge, nowUtc),
                "OTP mới đã được gửi đến email ca trưởng.");
        }

        private static string? ValidateFoundationRequest(OtpRequestDto request)
        {
            var actionType = request.ActionType?.Trim();
            var targetType = request.TargetType?.Trim();

            if (!string.Equals(actionType, OtpConstants.ActionTypes.CashDifference, StringComparison.OrdinalIgnoreCase))
                return "Issue #89 chỉ hỗ trợ ActionType CASH_DIFFERENCE.";

            if (!string.Equals(targetType, OtpConstants.TargetTypes.Shifts, StringComparison.OrdinalIgnoreCase))
                return "Issue #89 chỉ hỗ trợ TargetType shifts.";

            if (string.IsNullOrWhiteSpace(request.Reason))
                return "Vui lòng nhập lý do yêu cầu OTP.";

            return null;
        }

        private static string? EnsurePendingChallenge(OtpChallenge challenge, DateTime nowUtc)
        {
            if (challenge.Status == OtpConstants.Statuses.Pending && challenge.ExpiresAt <= nowUtc)
            {
                challenge.Status = OtpConstants.Statuses.Expired;
                return "OTP đã hết hạn. Vui lòng gửi lại OTP.";
            }

            if (challenge.Status != OtpConstants.Statuses.Pending)
            {
                return challenge.Status switch
                {
                    OtpConstants.Statuses.Approved => "OTP đã được xác nhận, không thể xác nhận lại.",
                    OtpConstants.Statuses.Used => "OTP đã được sử dụng, không thể dùng lại.",
                    OtpConstants.Statuses.Locked => "Yêu cầu OTP đã bị khóa.",
                    OtpConstants.Statuses.Expired => "OTP đã hết hạn. Vui lòng gửi lại OTP.",
                    OtpConstants.Statuses.Cancelled => "Yêu cầu OTP đã bị hủy.",
                    _ => "Yêu cầu OTP không ở trạng thái có thể xác nhận."
                };
            }

            if (challenge.FailedAttempts >= OtpConstants.MaxFailedAttempts)
            {
                challenge.Status = OtpConstants.Statuses.Locked;
                challenge.LockedAt = nowUtc;
                return "Yêu cầu OTP đã bị khóa do nhập sai quá số lần cho phép.";
            }

            return null;
        }

        private static ServiceResult<OtpChallengeResponseDto> Failure(
            string message,
            OtpChallenge challenge,
            DateTime nowUtc)
        {
            return new ServiceResult<OtpChallengeResponseDto>
            {
                IsSuccess = false,
                Message = message,
                Data = MapResponse(challenge, nowUtc)
            };
        }

        private static OtpChallengeResponseDto MapResponse(OtpChallenge challenge, DateTime nowUtc)
        {
            var expiresInSeconds = Math.Max(0, (int)Math.Ceiling((challenge.ExpiresAt - nowUtc).TotalSeconds));
            var resendAvailableAt = challenge.LastSentAt.AddSeconds(OtpConstants.ResendCooldownSeconds);
            var resendAvailableInSeconds = Math.Max(0, (int)Math.Ceiling((resendAvailableAt - nowUtc).TotalSeconds));
            var remainingAttempts = Math.Max(0, OtpConstants.MaxFailedAttempts - challenge.FailedAttempts);

            return new OtpChallengeResponseDto
            {
                OtpChallengePublicId = challenge.PublicId,
                Status = challenge.Status,
                ExpiresInSeconds = expiresInSeconds,
                ResendAvailableInSeconds = resendAvailableInSeconds,
                RemainingAttempts = remainingAttempts
            };
        }

        private static string BuildTargetLabel(OtpChallenge challenge)
        {
            if (challenge.TargetId.HasValue)
                return $"WorkShift #{challenge.TargetId.Value}";

            if (challenge.WorkShiftId.HasValue)
                return $"WorkShift #{challenge.WorkShiftId.Value}";

            return "WorkShift chưa xác định";
        }

        private static string GenerateOtpCode()
        {
            var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
            return value.ToString($"D{OtpConstants.CodeLength}");
        }
    }
}
