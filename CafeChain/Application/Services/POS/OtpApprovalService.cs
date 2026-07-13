using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Accounts;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Operations;
using CafeChain.Models.Staffs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace CafeChain.Application.Services.POS
{
    public class OtpApprovalService : IOtpApprovalService
    {
        private readonly IOtpChallengeRepository _repository;
        private readonly IWorkShiftRepository _workShiftRepository;
        private readonly IEmailService _emailService;
        private readonly IOtpCodeGenerator _codeGenerator;
        private readonly IOtpPayloadFingerprintService _fingerprint;
        private readonly ILogger<OtpApprovalService> _logger;
        private readonly IWebHostEnvironment _environment;

        public OtpApprovalService(
            IOtpChallengeRepository repository,
            IWorkShiftRepository workShiftRepository,
            IEmailService emailService,
            IOtpCodeGenerator codeGenerator,
            IOtpPayloadFingerprintService fingerprint,
            ILogger<OtpApprovalService> logger,
            IWebHostEnvironment environment)
        {
            _repository = repository;
            _workShiftRepository = workShiftRepository;
            _emailService = emailService;
            _codeGenerator = codeGenerator;
            _fingerprint = fingerprint;
            _logger = logger;
            _environment = environment;
        }

        public async Task<ServiceResult<OtpChallengeResponseDto>> RequestOtpAsync(
            OtpRequestDto request,
            int requestedByStaffId,
            int storeId)
        {
            if (request == null)
                return ServiceResult<OtpChallengeResponseDto>.Failure("Thiếu dữ liệu yêu cầu OTP.");

            var actionType = NormalizeActionType(request.ActionType);
            if (actionType == null)
                return ServiceResult<OtpChallengeResponseDto>.Failure(
                    "ActionType không hỗ trợ. Dùng CASH_DIFFERENCE, CLOSE_SHIFT_EXCEPTION hoặc OPEN_SHIFT_LATE.");

            if (!string.Equals(request.TargetType?.Trim(), OtpConstants.TargetTypes.Shifts, StringComparison.OrdinalIgnoreCase))
                return ServiceResult<OtpChallengeResponseDto>.Failure("Chỉ hỗ trợ TargetType shifts.");

            if (string.IsNullOrWhiteSpace(request.Reason))
                return ServiceResult<OtpChallengeResponseDto>.Failure("Vui lòng nhập lý do yêu cầu OTP.");

            var build = await BuildChallengeContextAsync(request, actionType, requestedByStaffId, storeId);
            if (build.Error != null)
                return ServiceResult<OtpChallengeResponseDto>.Failure(build.Error);

            var requester = await _repository.GetRequestingStaffAsync(requestedByStaffId, storeId);
            if (requester == null)
                return ServiceResult<OtpChallengeResponseDto>.Failure("Không tìm thấy nhân viên yêu cầu hợp lệ tại cửa hàng này.");

            var nowUtc = DateTime.UtcNow;
            var targetId = build.TargetId!.Value;
            var workShiftId = build.WorkShiftId;

            await _repository.BeginTransactionAsync();
            try
            {
                var existing = await _repository.FindActiveChallengeAsync(
                    storeId,
                    requestedByStaffId,
                    actionType,
                    OtpConstants.TargetTypes.Shifts,
                    targetId,
                    nowUtc);

                if (existing != null)
                {
                    await _repository.CommitTransactionAsync();
                    return ServiceResult<OtpChallengeResponseDto>.Success(
                        MapResponse(existing, nowUtc, wasExistingActive: true),
                        "Đã có yêu cầu OTP đang hiệu lực. Dùng Gửi lại OTP nếu cần mã mới.");
                }

                var approver = await _repository.GetOtpApproverAsync(storeId, requestedByStaffId, nowUtc);
                if (approver == null || string.IsNullOrWhiteSpace(approver.Account?.Email))
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<OtpChallengeResponseDto>.Failure(
                        "Không có người duyệt OTP khác (Ca trưởng/QL chi nhánh) đang hoạt động có email tại cửa hàng. " +
                        "Không cho phép tự duyệt.",
                        errorCode: OtpConstants.ErrorCodes.NoEligibleApprover);
                }

                if (approver.StaffId == requestedByStaffId)
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<OtpChallengeResponseDto>.Failure(
                        "Không được tự gửi OTP cho chính mình.",
                        errorCode: OtpConstants.ErrorCodes.NoEligibleApprover);
                }

                var approverEmail = approver.Account.Email.Trim();
                if (!IsPlausibleEmail(approverEmail))
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<OtpChallengeResponseDto>.Failure(
                        "Email người duyệt OTP không hợp lệ trong CSDL. Vui lòng sửa Account.Email trong Admin.");
                }

                var store = await _repository.GetStoreAsync(storeId);
                if (store == null)
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<OtpChallengeResponseDto>.Failure("Không tìm thấy cửa hàng cho yêu cầu OTP.");
                }

                var reason = request.Reason.Trim();
                var otpCode = _codeGenerator.Generate();
                var challenge = new OtpChallenge
                {
                    PublicId = Guid.NewGuid(),
                    StoreId = storeId,
                    WorkShiftId = workShiftId,
                    RequestedByStaffId = requestedByStaffId,
                    ApproverStaffId = approver.StaffId,
                    ActionType = actionType,
                    TargetType = OtpConstants.TargetTypes.Shifts,
                    TargetId = targetId,
                    Reason = reason,
                    PayloadFingerprint = build.Fingerprint!,
                    OtpHash = BCrypt.Net.BCrypt.HashPassword(otpCode),
                    ExpiresAt = nowUtc.AddMinutes(OtpConstants.TtlMinutes),
                    LastSentAt = nowUtc,
                    CreatedAt = nowUtc,
                    Status = OtpConstants.Statuses.Pending,
                    OldValueJson = request.OldValueJson,
                    NewValueJson = request.NewValueJson
                };

                try
                {
                    await _repository.AddAsync(challenge);
                    await _repository.CommitTransactionAsync();
                }
                catch (DbUpdateException)
                {
                    await _repository.RollbackTransactionAsync();
                    var raced = await _repository.FindActiveChallengeAsync(
                        storeId, requestedByStaffId, actionType,
                        OtpConstants.TargetTypes.Shifts, targetId, nowUtc);
                    if (raced != null)
                    {
                        return ServiceResult<OtpChallengeResponseDto>.Success(
                            MapResponse(raced, nowUtc, wasExistingActive: true),
                            "Đã có yêu cầu OTP đang hiệu lực. Dùng Gửi lại OTP nếu cần mã mới.");
                    }

                    throw;
                }

                var approverRoleLabel = ResolveApproverRoleLabel(approver);
                var actionLabel = ResolveActionLabel(actionType);
                var subject = $"[Xác nhận {approverRoleLabel}] {actionLabel} - {store.Name}";
                var body = _emailService.BuildOperationalOtpEmail(
                    otpCode,
                    store.Name,
                    build.TargetLabel!,
                    requester.FullName,
                    actionLabel,
                    challenge.Reason,
                    nowUtc,
                    OtpConstants.TtlMinutes);

                try
                {
                    await _emailService.SendAsync(approverEmail, subject, body);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "OTP_EMAIL_SEND_FAILED | StoreId={StoreId} | RequestedByStaffId={RequestedByStaffId} | ApproverStaffId={ApproverStaffId} | To={To}",
                        storeId, requestedByStaffId, approver.StaffId, MaskEmail(approverEmail));

                    if (_environment.IsDevelopment())
                    {
                        _logger.LogWarning(
                            "OTP_DEV_CAPTURE | PublicId={PublicId} | ApproverStaffId={ApproverStaffId} | To={To}",
                            challenge.PublicId, approver.StaffId, MaskEmail(approverEmail));

                        return ServiceResult<OtpChallengeResponseDto>.Success(
                            MapResponse(challenge, nowUtc),
                            $"SMTP lỗi. Development capture OTP {approverRoleLabel} {MaskEmail(approverEmail)}: {otpCode}");
                    }

                    challenge.Status = OtpConstants.Statuses.Cancelled;
                    challenge.CancelledAt = DateTime.UtcNow;
                    await _repository.SaveChangesAsync();

                    return ServiceResult<OtpChallengeResponseDto>.Failure(
                        "Không gửi được OTP ca trưởng. Vui lòng kiểm tra cấu hình email.",
                        errorCode: OtpConstants.ErrorCodes.EmailFailed);
                }

                return ServiceResult<OtpChallengeResponseDto>.Success(
                    MapResponse(challenge, nowUtc),
                    $"OTP đã được gửi đến {approverRoleLabel} ({MaskEmail(approverEmail)}). Mã gồm 6 ký tự chữ in hoa và số.");
            }
            catch
            {
                await _repository.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<ServiceResult<OtpChallengeResponseDto>> VerifyOtpAsync(OtpVerifyDto request)
        {
            if (request == null || request.OtpChallengePublicId == Guid.Empty)
                return ServiceResult<OtpChallengeResponseDto>.Failure("Thiếu mã yêu cầu OTP.");

            var code = _codeGenerator.NormalizeAndValidate(request.OtpCode);
            if (code == null)
                return ServiceResult<OtpChallengeResponseDto>.Failure(
                    "Mã OTP không hợp lệ. Nhập đúng 6 ký tự (chữ in hoa A–Z và số, không gồm O/0/I/1).");

            var nowUtc = DateTime.UtcNow;

            try
            {
                await _repository.BeginTransactionAsync();

                var challenge = await _repository.GetByPublicIdForUpdateAsync(request.OtpChallengePublicId);
                if (challenge == null)
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<OtpChallengeResponseDto>.Failure("Không tìm thấy yêu cầu OTP.");
                }

                if (challenge.ApproverStaffId == challenge.RequestedByStaffId)
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<OtpChallengeResponseDto>.Failure(
                        "Challenge không hợp lệ (self-approval).",
                        errorCode: OtpConstants.ErrorCodes.NoEligibleApprover);
                }

                var statusFailure = EnsurePendingChallenge(challenge, nowUtc);
                if (statusFailure != null)
                {
                    await _repository.SaveChangesAsync();
                    await _repository.CommitTransactionAsync();
                    return Failure(statusFailure, challenge, nowUtc);
                }

                var isValidOtp = BCrypt.Net.BCrypt.Verify(code, challenge.OtpHash);
                if (!isValidOtp)
                {
                    challenge.FailedAttempts++;
                    if (challenge.FailedAttempts >= OtpConstants.MaxFailedAttempts)
                    {
                        challenge.Status = OtpConstants.Statuses.Locked;
                        challenge.LockedAt = nowUtc;
                        await _repository.SaveChangesAsync();
                        await _repository.CommitTransactionAsync();
                        return Failure("Yêu cầu OTP đã bị khóa do nhập sai quá số lần cho phép.", challenge, nowUtc);
                    }

                    await _repository.SaveChangesAsync();
                    await _repository.CommitTransactionAsync();
                    return Failure(
                        $"OTP không đúng. Bạn còn {OtpConstants.MaxFailedAttempts - challenge.FailedAttempts} lần thử.",
                        challenge,
                        nowUtc);
                }

                // One-winner: Pending → Approved
                if (challenge.Status != OtpConstants.Statuses.Pending)
                {
                    await _repository.RollbackTransactionAsync();
                    return Failure("OTP không còn ở trạng thái có thể xác nhận.", challenge, nowUtc);
                }

                challenge.Status = OtpConstants.Statuses.Approved;
                challenge.ApprovedAt = nowUtc;
                await _repository.SaveChangesAsync();
                await _repository.CommitTransactionAsync();

                return ServiceResult<OtpChallengeResponseDto>.Success(
                    MapResponse(challenge, nowUtc),
                    "Xác nhận OTP thành công.");
            }
            catch (DbUpdateConcurrencyException)
            {
                await _repository.RollbackTransactionAsync();
                return ServiceResult<OtpChallengeResponseDto>.Failure(
                    "OTP đang được xử lý bởi yêu cầu khác. Vui lòng thử lại.");
            }
            catch
            {
                await _repository.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<ServiceResult<OtpChallengeResponseDto>> ResendOtpAsync(OtpResendDto request)
        {
            if (request == null || request.OtpChallengePublicId == Guid.Empty)
                return ServiceResult<OtpChallengeResponseDto>.Failure("Thiếu mã yêu cầu OTP.");

            var nowUtc = DateTime.UtcNow;

            try
            {
                await _repository.BeginTransactionAsync();
                var challenge = await _repository.GetByPublicIdForUpdateAsync(request.OtpChallengePublicId);
                if (challenge == null)
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<OtpChallengeResponseDto>.Failure("Không tìm thấy yêu cầu OTP.");
                }

                if (challenge.Status != OtpConstants.Statuses.Pending)
                {
                    await _repository.RollbackTransactionAsync();
                    return Failure("Yêu cầu OTP hiện tại không thể gửi lại.", challenge, nowUtc);
                }

                if (challenge.ExpiresAt <= nowUtc)
                {
                    challenge.Status = OtpConstants.Statuses.Expired;
                    await _repository.SaveChangesAsync();
                    await _repository.CommitTransactionAsync();
                    return Failure("OTP đã hết hạn. Vui lòng tạo yêu cầu mới.", challenge, nowUtc);
                }

                if (challenge.ResendCount >= OtpConstants.MaxResendCount)
                {
                    await _repository.RollbackTransactionAsync();
                    return Failure("Yêu cầu OTP đã vượt quá số lần gửi lại cho phép.", challenge, nowUtc);
                }

                var nextAllowed = challenge.LastSentAt.AddSeconds(OtpConstants.ResendCooldownSeconds);
                if (nextAllowed > nowUtc)
                {
                    await _repository.RollbackTransactionAsync();
                    var waitSeconds = (int)Math.Ceiling((nextAllowed - nowUtc).TotalSeconds);
                    return Failure($"Vui lòng đợi {waitSeconds} giây trước khi gửi lại OTP.", challenge, nowUtc);
                }

                if (challenge.Store == null || challenge.ApproverStaff?.Account == null)
                {
                    await _repository.RollbackTransactionAsync();
                    return Failure("Yêu cầu OTP thiếu dữ liệu cửa hàng hoặc người duyệt.", challenge, nowUtc);
                }

                var otpCode = _codeGenerator.Generate();
                var subject = $"[Xác nhận ca trưởng] {ResolveActionLabel(challenge.ActionType)} - {challenge.Store.Name}";
                var body = _emailService.BuildOperationalOtpEmail(
                    otpCode,
                    challenge.Store.Name,
                    BuildTargetLabel(challenge),
                    challenge.RequestedByStaff?.FullName ?? $"Staff #{challenge.RequestedByStaffId}",
                    ResolveActionLabel(challenge.ActionType),
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
                        "OTP_EMAIL_RESEND_FAILED | PublicId={PublicId} | StoreId={StoreId}",
                        challenge.PublicId, challenge.StoreId);

                    if (_environment.IsDevelopment())
                    {
                        // Keep Pending and rotate hash even if email fails in Development so tests can proceed.
                    }
                    else
                    {
                        await _repository.RollbackTransactionAsync();
                        return Failure("Không gửi lại được OTP ca trưởng. Vui lòng kiểm tra cấu hình email.", challenge, nowUtc);
                    }
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
                await _repository.CommitTransactionAsync();

                var msg = "OTP mới đã được gửi đến email ca trưởng. Mã cũ không còn hiệu lực.";
                if (_environment.IsDevelopment())
                    msg += $" Development code: {otpCode}";

                return ServiceResult<OtpChallengeResponseDto>.Success(MapResponse(challenge, nowUtc), msg);
            }
            catch (DbUpdateConcurrencyException)
            {
                await _repository.RollbackTransactionAsync();
                return ServiceResult<OtpChallengeResponseDto>.Failure("OTP đang được xử lý. Vui lòng thử lại.");
            }
            catch
            {
                await _repository.RollbackTransactionAsync();
                throw;
            }
        }

        private async Task<(string? Error, string? Fingerprint, int? TargetId, int? WorkShiftId, string? TargetLabel)>
            BuildChallengeContextAsync(
                OtpRequestDto request,
                string actionType,
                int requestedByStaffId,
                int storeId)
        {
            if (actionType == OtpConstants.ActionTypes.CashDifference)
            {
                var workShiftId = request.WorkShiftId ?? request.TargetId;
                if (!workShiftId.HasValue || workShiftId.Value <= 0)
                    return ("WorkShiftId/TargetId không hợp lệ cho CASH_DIFFERENCE.", null, null, null, null);

                var fingerprint = _fingerprint.BuildCashDifferenceFingerprint(
                    storeId, requestedByStaffId, workShiftId.Value,
                    request.ActualEndingCash, request.Reason);

                return (null, fingerprint, workShiftId.Value, workShiftId.Value, $"WorkShift #{workShiftId.Value}");
            }

            if (actionType == OtpConstants.ActionTypes.CloseShiftException)
            {
                var workShiftId = request.WorkShiftId ?? request.TargetId;
                if (!workShiftId.HasValue || workShiftId.Value <= 0)
                    return ("WorkShiftId/TargetId không hợp lệ cho CLOSE_SHIFT_EXCEPTION.", null, null, null, null);

                if (string.IsNullOrWhiteSpace(request.ExceptionReason))
                    return ("Vui lòng nhập lý do đóng ca ngoại lệ cho OTP.", null, null, null, null);

                var offline = request.OfflineQueueSummary ?? new OfflineQueueSummaryDto();
                if (offline.OfflineOrderCount < 0 || offline.EstimatedTotal < 0 || offline.LocalCashTotal < 0)
                    return ("Tóm tắt đơn offline không hợp lệ.", null, null, null, null);

                var fingerprint = _fingerprint.BuildCloseShiftExceptionFingerprint(
                    storeId,
                    requestedByStaffId,
                    workShiftId.Value,
                    request.ActualEndingCash,
                    request.ExceptionReason,
                    request.DiscrepancyReason,
                    offline);

                return (null, fingerprint, workShiftId.Value, workShiftId.Value, $"WorkShift #{workShiftId.Value}");
            }

            if (actionType == OtpConstants.ActionTypes.OpenShiftLate)
            {
                // No WorkShift yet — target is the actor staff id.
                var scheduled = await ResolveScheduledStartCanonicalAsync(requestedByStaffId);
                var reason = request.Reason;
                var fingerprint = _fingerprint.BuildOpenShiftLateFingerprint(
                    storeId,
                    requestedByStaffId,
                    request.StartingCash,
                    reason,
                    scheduled);

                return (null, fingerprint, requestedByStaffId, null, $"Staff #{requestedByStaffId}");
            }

            return ("ActionType không hỗ trợ.", null, null, null, null);
        }

        private async Task<string> ResolveScheduledStartCanonicalAsync(int staffId)
        {
            var staffShift = await _workShiftRepository.GetTodayStaffShiftAsync(staffId);
            if (staffShift?.Shift == null)
                return "none";

            var start = DateTime.Today.Add(staffShift.Shift.StartTime);
            return start.ToString("yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string? NormalizeActionType(string? actionType)
        {
            if (string.IsNullOrWhiteSpace(actionType))
                return null;

            var value = actionType.Trim();
            if (string.Equals(value, OtpConstants.ActionTypes.CashDifference, StringComparison.OrdinalIgnoreCase))
                return OtpConstants.ActionTypes.CashDifference;
            if (string.Equals(value, OtpConstants.ActionTypes.CloseShiftException, StringComparison.OrdinalIgnoreCase))
                return OtpConstants.ActionTypes.CloseShiftException;
            if (string.Equals(value, OtpConstants.ActionTypes.OpenShiftLate, StringComparison.OrdinalIgnoreCase))
                return OtpConstants.ActionTypes.OpenShiftLate;
            return null;
        }

        private static string ResolveActionLabel(string actionType)
        {
            return actionType switch
            {
                OtpConstants.ActionTypes.CashDifference => "Xác nhận đóng ca có chênh lệch",
                OtpConstants.ActionTypes.CloseShiftException => "Xác nhận đóng ca ngoại lệ",
                OtpConstants.ActionTypes.OpenShiftLate => "Xác nhận mở ca trễ",
                _ => "Xác nhận OTP"
            };
        }

        private static string? EnsurePendingChallenge(OtpChallenge challenge, DateTime nowUtc)
        {
            if (challenge.Status == OtpConstants.Statuses.Pending && challenge.ExpiresAt <= nowUtc)
            {
                challenge.Status = OtpConstants.Statuses.Expired;
                return "OTP đã hết hạn. Vui lòng gửi lại OTP hoặc tạo yêu cầu mới.";
            }

            if (challenge.Status != OtpConstants.Statuses.Pending)
            {
                return challenge.Status switch
                {
                    OtpConstants.Statuses.Approved => "OTP đã được xác nhận, không thể xác nhận lại.",
                    OtpConstants.Statuses.Used => "OTP đã được sử dụng, không thể dùng lại.",
                    OtpConstants.Statuses.Locked => "Yêu cầu OTP đã bị khóa.",
                    OtpConstants.Statuses.Expired => "OTP đã hết hạn. Vui lòng tạo yêu cầu mới.",
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

        private static OtpChallengeResponseDto MapResponse(
            OtpChallenge challenge,
            DateTime nowUtc,
            bool wasExistingActive = false)
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
                RemainingAttempts = remainingAttempts,
                WasExistingActive = wasExistingActive
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

        private static string MaskEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return "(none)";
            var value = email.Trim();
            var at = value.IndexOf('@');
            if (at <= 1) return "***";
            return value[0] + "***" + value.Substring(at);
        }

        private static bool IsPlausibleEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                if (!string.Equals(addr.Address, email, StringComparison.OrdinalIgnoreCase))
                    return false;
                var host = addr.Host ?? string.Empty;
                if (host.IndexOf('.') < 1)
                    return false;
                if (host.Equals("gmal.com", StringComparison.OrdinalIgnoreCase)
                    || host.Equals("gmial.com", StringComparison.OrdinalIgnoreCase)
                    || host.Equals("gamil.com", StringComparison.OrdinalIgnoreCase))
                    return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveApproverRoleLabel(Staff approver)
        {
            var roleNames = approver.Account?.AccountRoles
                .Where(ar => ar.Role != null && ar.Role.Active)
                .Select(ar => ar.Role!.Name)
                .ToList() ?? new List<string>();

            if (roleNames.Contains(RoleConstants.ShiftSupervisor))
                return RoleConstants.ShiftSupervisor;
            if (roleNames.Contains(RoleConstants.StoreManager))
                return RoleConstants.StoreManager;
            return "người duyệt";
        }
    }
}
