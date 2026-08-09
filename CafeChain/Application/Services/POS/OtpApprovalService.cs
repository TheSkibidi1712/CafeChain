using CafeChain.Application.Constants;
using CafeChain.Application.Authorization;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Accounts;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Options;
using CafeChain.Application.Results;
using CafeChain.Application.Tools;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Operations;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
        private readonly IAdminPermissionService? _permissions;
        private readonly TimeProvider _timeProvider;
        private readonly WorkShiftOptions _workShiftOptions;
        private readonly IWorkShiftAuditService? _audit;
        private readonly IStaffNotificationRepository? _staffNotifications;
        private readonly IOperationalOtpNotificationPublisher? _otpNotificationPublisher;
        private readonly IOtpProtectedPayloadService? _otpProtectedPayload;

        public OtpApprovalService(
            IOtpChallengeRepository repository,
            IWorkShiftRepository workShiftRepository,
            IEmailService emailService,
            IOtpCodeGenerator codeGenerator,
            IOtpPayloadFingerprintService fingerprint,
            ILogger<OtpApprovalService> logger,
            IWebHostEnvironment environment,
            IAdminPermissionService? permissions = null,
            TimeProvider? timeProvider = null,
            IOptions<WorkShiftOptions>? workShiftOptions = null,
            IWorkShiftAuditService? audit = null,
            IStaffNotificationRepository? staffNotifications = null,
            IOperationalOtpNotificationPublisher? otpNotificationPublisher = null,
            IOtpProtectedPayloadService? otpProtectedPayload = null)
        {
            _repository = repository;
            _workShiftRepository = workShiftRepository;
            _emailService = emailService;
            _codeGenerator = codeGenerator;
            _fingerprint = fingerprint;
            _logger = logger;
            _environment = environment;
            _permissions = permissions;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _workShiftOptions = workShiftOptions?.Value ?? new WorkShiftOptions();
            _audit = audit;
            _staffNotifications = staffNotifications;
            _otpNotificationPublisher = otpNotificationPublisher;
            _otpProtectedPayload = otpProtectedPayload;
        }

        public async Task<ServiceResult<OtpChallengeResponseDto>> GetCurrentOpenPosOtpStateAsync(
            int requestedByStaffId,
            int storeId)
        {
            var nowUtc = UtcNow;
            var challenge = await _repository.FindLatestOpenShiftChallengeAsync(
                storeId,
                requestedByStaffId,
                nowUtc.AddMinutes(-OtpConstants.RateLimitWindowMinutes));
            if (challenge == null)
            {
                return ServiceResult<OtpChallengeResponseDto>.Success(new OtpChallengeResponseDto
                {
                    HasActiveChallenge = false,
                    Status = "IDLE"
                });
            }

            var response = MapResponse(challenge, nowUtc);
            if (response.Status == OtpConstants.Statuses.Pending && challenge.ExpiresAt <= nowUtc)
                response.Status = OtpConstants.Statuses.Expired;
            return ServiceResult<OtpChallengeResponseDto>.Success(response);
        }

        public async Task<ServiceResult<OtpChallengeResponseDto>> GetCurrentTerminalRegistrationOtpStateAsync(
            int requestedByStaffId,
            int storeId)
        {
            if (requestedByStaffId <= 0 || storeId <= 0)
                return ServiceResult<OtpChallengeResponseDto>.Failure("Ngữ cảnh nhân viên/cửa hàng không hợp lệ.");
            var nowUtc = UtcNow;
            var challenge = await _repository.FindLatestTerminalRegistrationChallengeAsync(
                storeId,
                requestedByStaffId,
                nowUtc.AddDays(-1));
            if (challenge == null)
            {
                return ServiceResult<OtpChallengeResponseDto>.Success(new OtpChallengeResponseDto
                {
                    HasActiveChallenge = false,
                    Status = "IDLE"
                });
            }
            var response = MapResponse(challenge, nowUtc);
            if (challenge.Status == OtpConstants.Statuses.Pending && challenge.ExpiresAt <= nowUtc)
                response.Status = OtpConstants.Statuses.Expired;
            return ServiceResult<OtpChallengeResponseDto>.Success(response);
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
                    "ActionType không hỗ trợ. Dùng REGISTER_POS_TERMINAL, OPEN_SHIFT_OUTSIDE_SCHEDULE, CASH_DIFFERENCE hoặc CLOSE_SHIFT_EXCEPTION.");

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

            var nowUtc = UtcNow;
            var targetId = build.TargetId!.Value;
            var workShiftId = build.WorkShiftId;
            StaffNotification? notification = null;
            var createdNotifications = new List<StaffNotification>();

            await _repository.BeginTransactionAsync();
            try
            {
                // Unique index UX_OtpChallenges_OneActivePerActorActionTarget keys on Status only
                // (Pending/Approved), not ExpiresAt. Expired-but-still-Pending rows block inserts
                // and must be closed before a new challenge can be created.
                await _repository.ExpireStaleActiveChallengesAsync(
                    storeId,
                    requestedByStaffId,
                    actionType,
                    OtpConstants.TargetTypes.Shifts,
                    targetId,
                    nowUtc);

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

                var rateLimitSinceUtc = nowUtc.AddMinutes(-OtpConstants.RateLimitWindowMinutes);
                var recentStaffChallenges = await _repository.GetRecentChallengeCountForStaffAsync(
                    requestedByStaffId,
                    rateLimitSinceUtc);
                if (recentStaffChallenges >= OtpConstants.MaxChallengesPerStaffWindow)
                {
                    await _repository.RollbackTransactionAsync();
                    _logger.LogWarning(
                        "OTP_REQUEST_RATE_LIMITED | StoreId={StoreId} StaffId={StaffId} WindowMinutes={WindowMinutes}",
                        storeId, requestedByStaffId, OtpConstants.RateLimitWindowMinutes);
                    return ServiceResult<OtpChallengeResponseDto>.Failure(
                        "Bạn đã tạo quá nhiều yêu cầu OTP. Vui lòng thử lại sau 15 phút.",
                        errorCode: OtpConstants.ErrorCodes.RateLimited);
                }

                var normalizedTerminalId = request.TerminalId?.Trim();
                if (!string.IsNullOrWhiteSpace(normalizedTerminalId))
                {
                    var recentTerminalChallenges = await _repository.GetRecentChallengeCountForTerminalAsync(
                        normalizedTerminalId,
                        rateLimitSinceUtc);
                    if (recentTerminalChallenges >= OtpConstants.MaxChallengesPerTerminalWindow)
                    {
                        await _repository.RollbackTransactionAsync();
                        _logger.LogWarning(
                            "OTP_TERMINAL_RATE_LIMITED | StoreId={StoreId} TerminalId={TerminalId} WindowMinutes={WindowMinutes}",
                            storeId, normalizedTerminalId, OtpConstants.RateLimitWindowMinutes);
                        return ServiceResult<OtpChallengeResponseDto>.Failure(
                            "Terminal đã tạo quá nhiều yêu cầu OTP. Vui lòng thử lại sau 15 phút.",
                            errorCode: OtpConstants.ErrorCodes.RateLimited);
                    }
                }

                if (!string.IsNullOrWhiteSpace(request.ClientIpHash))
                {
                    var recentIpChallenges = await _repository.GetRecentChallengeCountForIpAsync(
                        request.ClientIpHash,
                        rateLimitSinceUtc);
                    if (recentIpChallenges >= OtpConstants.MaxChallengesPerIpWindow)
                    {
                        await _repository.RollbackTransactionAsync();
                        _logger.LogWarning(
                            "OTP_IP_RATE_LIMITED | StoreId={StoreId} StaffId={StaffId} WindowMinutes={WindowMinutes}",
                            storeId, requestedByStaffId, OtpConstants.RateLimitWindowMinutes);
                        return ServiceResult<OtpChallengeResponseDto>.Failure(
                            "Thiết bị hoặc kết nối đã tạo quá nhiều yêu cầu OTP. Vui lòng thử lại sau 15 phút.",
                            errorCode: OtpConstants.ErrorCodes.RateLimited);
                    }
                }

                if (!string.IsNullOrWhiteSpace(request.DeviceFingerprintHash))
                {
                    var recentDeviceChallenges = await _repository.GetRecentChallengeCountForDeviceAsync(
                        request.DeviceFingerprintHash,
                        rateLimitSinceUtc);
                    if (recentDeviceChallenges >= OtpConstants.MaxChallengesPerDeviceWindow)
                    {
                        await _repository.RollbackTransactionAsync();
                        _logger.LogWarning(
                            "OTP_DEVICE_RATE_LIMITED | StoreId={StoreId} StaffId={StaffId} WindowMinutes={WindowMinutes}",
                            storeId, requestedByStaffId, OtpConstants.RateLimitWindowMinutes);
                        return ServiceResult<OtpChallengeResponseDto>.Failure(
                            "Thiết bị hoặc kết nối đã tạo quá nhiều yêu cầu OTP. Vui lòng thử lại sau 15 phút.",
                            errorCode: OtpConstants.ErrorCodes.RateLimited);
                    }
                }

                var approver = _permissions == null
                    ? await _repository.GetOtpApproverAsync(storeId, requestedByStaffId, nowUtc)
                    : await ResolvePermissionApproverAsync(actionType, storeId, requestedByStaffId);
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
                    TerminalId = string.IsNullOrWhiteSpace(request.TerminalId) ? null : request.TerminalId.Trim(),
                    TerminalName = string.IsNullOrWhiteSpace(request.TerminalName)
                        ? (string.IsNullOrWhiteSpace(request.TerminalId) ? null : request.TerminalId.Trim())
                        : request.TerminalName.Trim(),
                    RequestKey = string.IsNullOrWhiteSpace(request.RequestKey) ? null : request.RequestKey.Trim(),
                    ClientIpHash = NormalizeSecurityHash(request.ClientIpHash),
                    DeviceFingerprintHash = NormalizeSecurityHash(request.DeviceFingerprintHash),
                    OldValueJson = request.OldValueJson,
                    NewValueJson = request.NewValueJson
                };
                if (_otpProtectedPayload != null)
                {
                    challenge.ProtectedOtpPayload = _otpProtectedPayload.Protect(
                        challenge.PublicId,
                        challenge.ApproverStaffId,
                        otpCode,
                        challenge.ExpiresAt);
                }

                try
                {
                    await _repository.AddAsync(challenge);
                    if (_staffNotifications != null)
                    {
                        notification = BuildOtpNotification(
                            challenge,
                            requester.FullName,
                            store.Name,
                            actionLabel: ResolveActionLabel(actionType),
                            nowUtc);
                        _staffNotifications.Add(notification);
                        createdNotifications.Add(notification);
                        if (actionType == OtpConstants.ActionTypes.RegisterTerminal)
                        {
                            foreach (var reviewerNotification in await BuildTerminalRejectionNotificationsAsync(
                                challenge,
                                requester.FullName,
                                store.Name,
                                approver.StaffId,
                                nowUtc))
                            {
                                _staffNotifications.Add(reviewerNotification);
                                createdNotifications.Add(reviewerNotification);
                            }
                        }
                        await _staffNotifications.SaveChangesAsync();
                    }
                    await _repository.CommitTransactionAsync();
                }
                catch (DbUpdateException ex)
                {
                    await _repository.RollbackTransactionAsync();

                    // Concurrent create, or stale Pending/Approved still holding the unique index.
                    await _repository.BeginTransactionAsync();
                    try
                    {
                        await _repository.ExpireStaleActiveChallengesAsync(
                            storeId, requestedByStaffId, actionType,
                            OtpConstants.TargetTypes.Shifts, targetId, nowUtc);

                        var raced = await _repository.FindActiveChallengeAsync(
                            storeId, requestedByStaffId, actionType,
                            OtpConstants.TargetTypes.Shifts, targetId, nowUtc);
                        if (raced != null)
                        {
                            await _repository.CommitTransactionAsync();
                            return ServiceResult<OtpChallengeResponseDto>.Success(
                                MapResponse(raced, nowUtc, wasExistingActive: true),
                                "Đã có yêu cầu OTP đang hiệu lực. Dùng Gửi lại OTP nếu cần mã mới.");
                        }

                        await _repository.CommitTransactionAsync();
                    }
                    catch
                    {
                        await _repository.RollbackTransactionAsync();
                    }

                    _logger.LogWarning(
                        ex,
                        "OTP_REQUEST_UNIQUE_CONFLICT | StoreId={StoreId} | StaffId={StaffId} | Action={Action} | TargetId={TargetId}",
                        storeId, requestedByStaffId, actionType, targetId);

                    return ServiceResult<OtpChallengeResponseDto>.Failure(
                        "Đang có yêu cầu OTP khác cho thao tác này. Vui lòng dùng Gửi lại OTP hoặc thử lại sau vài giây.");
                }

                // The internal notification is authoritative and should reach
                // the approver immediately. SMTP delivery is a secondary channel.
                await PublishChangedSafeAsync(challenge, createdNotifications, "Created");

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

                    var safeDeliveryError = IsSmtpPasswordNotConfigured(ex)
                        ? "Gmail chưa được cấu hình."
                        : "Không gửi được Gmail.";
                    await ResolveFailedDeliveryAsync(notification, safeDeliveryError);
                    await _repository.SaveChangesAsync();
                    await PublishChangedSafeAsync(challenge, notification, "DeliveryUpdated");

                    var fallbackResponse = MapResponse(challenge, nowUtc);
                    fallbackResponse.DeliveryStatus = OtpDeliveryStatuses.InternalNotificationOnly;
                    fallbackResponse.MaskedRecipientEmail = MaskEmail(approverEmail);
                    return ServiceResult<OtpChallengeResponseDto>.Success(
                        fallbackResponse,
                        "OTP vẫn còn hiệu lực trong thông báo nội bộ; kênh email gửi thất bại.");
                }

                if (notification != null && _staffNotifications != null)
                {
                    notification.EmailAttempted = true;
                    notification.EmailSent = true;
                    notification.EmailErrorSummary = null;
                    notification.UpdatedAt = UtcNow;
                    await _staffNotifications.SaveChangesAsync();
                    await PublishChangedSafeAsync(challenge, notification, "DeliveryUpdated");
                }
                var sentResponse = MapResponse(challenge, nowUtc);
                sentResponse.DeliveryStatus = OtpDeliveryStatuses.EmailSent;
                sentResponse.MaskedRecipientEmail = MaskEmail(approverEmail);
                return ServiceResult<OtpChallengeResponseDto>.Success(
                    sentResponse,
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
                    "Mã OTP không hợp lệ. Nhập đúng 6 ký tự (chữ in hoa A–Z và số, không gồm O/0/I/1).",
                    errorCode: OtpConstants.ErrorCodes.Invalid);

            var nowUtc = UtcNow;

            try
            {
                await _repository.BeginTransactionAsync();

                var challenge = await _repository.GetByPublicIdForUpdateAsync(request.OtpChallengePublicId);
                if (challenge == null)
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<OtpChallengeResponseDto>.Failure("Không tìm thấy yêu cầu OTP.");
                }

                challenge.ClientIpHash ??= NormalizeSecurityHash(request.ClientIpHash);
                challenge.DeviceFingerprintHash ??= NormalizeSecurityHash(request.DeviceFingerprintHash);

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
                    await ResolveOtpNotificationAsync(challenge, "Resolved");
                    return Failure(statusFailure, challenge, nowUtc);
                }

                var isValidOtp = BCrypt.Net.BCrypt.Verify(code, challenge.OtpHash);
                if (!isValidOtp)
                {
                    var wasLocked = OperationalOtpChallengePolicy.RegisterFailedAttempt(challenge, nowUtc);
                    _logger.LogWarning(
                        "OTP_VERIFY_FAILED | ChallengeId={ChallengeId} StoreId={StoreId} StaffId={StaffId} Action={Action} FailedAttempts={FailedAttempts}",
                        challenge.OtpChallengeId,
                        challenge.StoreId,
                        challenge.RequestedByStaffId,
                        challenge.ActionType,
                        challenge.FailedAttempts);
                    await WriteOtpAuditSafeAsync(
                        "OTP_VERIFY_FAILED",
                        challenge,
                        new
                        {
                            challenge.StoreId,
                            challenge.ActionType,
                            challenge.FailedAttempts,
                            challenge.ClientIpHash,
                            challenge.DeviceFingerprintHash
                        });
                    if (wasLocked)
                    {
                        await _repository.SaveChangesAsync();
                        await _repository.CommitTransactionAsync();
                        await ResolveOtpNotificationAsync(challenge, "Resolved");
                        return Failure(
                            "Yêu cầu OTP đã bị khóa do nhập sai quá số lần cho phép.",
                            challenge,
                            nowUtc,
                            OtpConstants.ErrorCodes.VerificationLocked);
                    }

                    await _repository.SaveChangesAsync();
                    await _repository.CommitTransactionAsync();
                    return Failure(
                        $"OTP không đúng. Bạn còn {OtpConstants.MaxFailedAttempts - challenge.FailedAttempts} lần thử.",
                        challenge,
                        nowUtc,
                        OtpConstants.ErrorCodes.Invalid);
                }

                // One-winner: Pending → Approved
                if (challenge.Status != OtpConstants.Statuses.Pending)
                {
                    await _repository.RollbackTransactionAsync();
                    return Failure("OTP không còn ở trạng thái có thể xác nhận.", challenge, nowUtc);
                }

                challenge.Status = OtpConstants.Statuses.Approved;
                challenge.ApprovedAt = nowUtc;
                challenge.ProtectedOtpPayload = null;
                await _repository.SaveChangesAsync();
                await _repository.CommitTransactionAsync();
                await ResolveOtpNotificationAsync(challenge, "Resolved");

                _logger.LogInformation(
                    "OTP_VERIFY_SUCCEEDED | ChallengeId={ChallengeId} StoreId={StoreId} StaffId={StaffId} ApproverStaffId={ApproverStaffId} Action={Action}",
                    challenge.OtpChallengeId,
                    challenge.StoreId,
                    challenge.RequestedByStaffId,
                    challenge.ApproverStaffId,
                    challenge.ActionType);
                await WriteOtpAuditSafeAsync(
                    "OTP_VERIFY_SUCCEEDED",
                    challenge,
                    new
                    {
                        challenge.StoreId,
                        challenge.ActionType,
                        challenge.ApproverStaffId,
                        challenge.ClientIpHash,
                        challenge.DeviceFingerprintHash,
                        ApprovedAtUtc = challenge.ApprovedAt
                    });

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

        public async Task<ServiceResult<OtpChallengeResponseDto>> VerifyOtpAsync(
            OtpVerifyDto request,
            int requestedByStaffId,
            int storeId)
        {
            var sinceUtc = UtcNow.AddMinutes(-OtpConstants.RateLimitWindowMinutes);
            var recentFailures = await _repository.GetRecentFailedAttemptsAsync(
                requestedByStaffId,
                sinceUtc);
            if (recentFailures >= 5)
            {
                _logger.LogWarning(
                    "OTP_VERIFY_RATE_LIMITED | StoreId={StoreId} StaffId={StaffId} RecentFailures={RecentFailures}",
                    storeId, requestedByStaffId, recentFailures);
                return ServiceResult<OtpChallengeResponseDto>.Failure(
                    "Quá nhiều lần nhập OTP sai. Vui lòng thử lại sau 15 phút.",
                    errorCode: OtpConstants.ErrorCodes.RateLimited);
            }

            if (!string.IsNullOrWhiteSpace(request?.ClientIpHash)
                && await _repository.GetRecentFailedAttemptsForIpAsync(request.ClientIpHash, sinceUtc)
                    >= OtpConstants.MaxFailedAttemptsPerIpWindow)
            {
                _logger.LogWarning(
                    "OTP_VERIFY_IP_RATE_LIMITED | StoreId={StoreId} StaffId={StaffId}",
                    storeId, requestedByStaffId);
                return ServiceResult<OtpChallengeResponseDto>.Failure(
                    "Quá nhiều lần xác nhận OTP không thành công từ kết nối này. Vui lòng thử lại sau 15 phút.",
                    errorCode: OtpConstants.ErrorCodes.RateLimited);
            }

            var challenge = request == null
                ? null
                : await _repository.GetByPublicIdAsync(request.OtpChallengePublicId);
            if (challenge == null
                || challenge.RequestedByStaffId != requestedByStaffId
                || challenge.StoreId != storeId)
            {
                return ServiceResult<OtpChallengeResponseDto>.Failure(
                    "Yêu cầu OTP không hợp lệ hoặc không thuộc phiên hiện tại.",
                    errorCode: OtpConstants.ErrorCodes.ContextMismatch);
            }

            var deviceHash = challenge.DeviceFingerprintHash ?? request.DeviceFingerprintHash;
            if (!string.IsNullOrWhiteSpace(deviceHash)
                && await _repository.GetRecentFailedAttemptsForDeviceAsync(deviceHash, sinceUtc)
                    >= OtpConstants.MaxFailedAttemptsPerDeviceWindow)
            {
                _logger.LogWarning(
                    "OTP_VERIFY_DEVICE_RATE_LIMITED | StoreId={StoreId} StaffId={StaffId}",
                    storeId, requestedByStaffId);
                return ServiceResult<OtpChallengeResponseDto>.Failure(
                    "Quá nhiều lần xác nhận OTP không thành công từ thiết bị này. Vui lòng thử lại sau 15 phút.",
                    errorCode: OtpConstants.ErrorCodes.RateLimited);
            }

            if (!await IsApproverStillEligibleForChallengeAsync(challenge))
            {
                return ServiceResult<OtpChallengeResponseDto>.Failure(
                    "Người duyệt không còn quyền hoặc không còn thuộc phạm vi cửa hàng.",
                    errorCode: OtpConstants.ErrorCodes.ApproverNoLongerEligible);
            }
            return await VerifyOtpAsync(request);
        }

        public async Task<ServiceResult<OtpChallengeResponseDto>> ResendOtpAsync(OtpResendDto request)
        {
            if (request == null || request.OtpChallengePublicId == Guid.Empty)
                return ServiceResult<OtpChallengeResponseDto>.Failure("Thiếu mã yêu cầu OTP.");

            var nowUtc = UtcNow;

            try
            {
                await _repository.BeginTransactionAsync();
                var challenge = await _repository.GetByPublicIdForUpdateAsync(request.OtpChallengePublicId);
                if (challenge == null)
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<OtpChallengeResponseDto>.Failure("Không tìm thấy yêu cầu OTP.");
                }

                if (challenge.Status is not (OtpConstants.Statuses.Pending or OtpConstants.Statuses.Locked))
                {
                    await _repository.RollbackTransactionAsync();
                    return Failure("Yêu cầu OTP hiện tại không thể gửi lại.", challenge, nowUtc);
                }

                if (challenge.ExpiresAt <= nowUtc)
                {
                    challenge.Status = OtpConstants.Statuses.Expired;
                    challenge.ProtectedOtpPayload = null;
                    await _repository.SaveChangesAsync();
                    await _repository.CommitTransactionAsync();
                    await ResolveOtpNotificationAsync(challenge, "Resolved");
                    return Failure("OTP đã hết hạn. Vui lòng tạo yêu cầu mới.", challenge, nowUtc);
                }

                if (challenge.ResendCount >= OtpConstants.MaxResendCount)
                {
                    await _repository.RollbackTransactionAsync();
                    return Failure("Yêu cầu OTP đã vượt quá số lần gửi lại cho phép.", challenge, nowUtc);
                }

                if (!OperationalOtpChallengePolicy.CanResend(challenge, nowUtc, out var waitSeconds))
                {
                    await _repository.RollbackTransactionAsync();
                    return Failure($"Vui lòng đợi {waitSeconds} giây trước khi gửi lại OTP.", challenge, nowUtc,
                        OtpConstants.ErrorCodes.ResendCooldown);
                }

                if (challenge.Store == null || challenge.ApproverStaff?.Account == null)
                {
                    await _repository.RollbackTransactionAsync();
                    return Failure("Yêu cầu OTP thiếu dữ liệu cửa hàng hoặc người duyệt.", challenge, nowUtc);
                }

                var otpCode = _codeGenerator.Generate();
                var actionLabel = ResolveActionLabel(challenge.ActionType);
                var subject = $"[Xác nhận ca trưởng] {actionLabel} - {challenge.Store.Name}";
                var body = _emailService.BuildOperationalOtpEmail(
                    otpCode,
                    challenge.Store.Name,
                    BuildTargetLabel(challenge),
                    challenge.RequestedByStaff?.FullName ?? $"Staff #{challenge.RequestedByStaffId}",
                    ResolveActionLabel(challenge.ActionType),
                    challenge.Reason,
                    nowUtc,
                    OtpConstants.TtlMinutes);

                challenge.OtpHash = BCrypt.Net.BCrypt.HashPassword(otpCode);
                challenge.ExpiresAt = nowUtc.AddMinutes(OtpConstants.TtlMinutes);
                challenge.LastSentAt = nowUtc;
                challenge.ResendCount++;
                OperationalOtpChallengePolicy.ResetAfterResend(challenge);
                challenge.ProtectedOtpPayload = _otpProtectedPayload?.Protect(
                    challenge.PublicId,
                    challenge.ApproverStaffId,
                    otpCode,
                    challenge.ExpiresAt);

                StaffNotification? notification = null;
                if (_staffNotifications != null)
                {
                    notification = await _staffNotifications.GetByDeduplicationKeyAsync(
                        OtpNotificationKey(challenge.PublicId));
                    if (notification != null)
                    {
                        notification.ResolvedAt = null;
                        notification.IsRead = false;
                        notification.ReadAt = null;
                        notification.EmailAttempted = true;
                        notification.EmailSent = false;
                        notification.EmailErrorSummary = null;
                        notification.UpdatedAt = nowUtc;
                        notification.Body =
                            $"{challenge.RequestedByStaff?.FullName ?? $"Staff #{challenge.RequestedByStaffId}"} " +
                            $"yêu cầu {actionLabel} tại {challenge.Store.Name}. Lý do: {challenge.Reason}. " +
                            "Chi tiết thời hạn và trạng thái được hiển thị trong thẻ OTP bên dưới.";
                        notification.MeaningfulVersion = challenge.ExpiresAt.Ticks.ToString(
                            System.Globalization.CultureInfo.InvariantCulture);
                    }
                }

                await _repository.SaveChangesAsync();
                await _repository.CommitTransactionAsync();
                await PublishChangedSafeAsync(challenge, notification, "Resent");

                var emailSent = false;
                try
                {
                    await _emailService.SendAsync(challenge.ApproverStaff.Account.Email.Trim(), subject, body);
                    emailSent = true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "OTP_EMAIL_RESEND_FAILED | PublicId={PublicId} | StoreId={StoreId}",
                        challenge.PublicId, challenge.StoreId);
                }

                if (notification != null && _staffNotifications != null)
                {
                    notification.EmailSent = emailSent;
                    notification.EmailErrorSummary = emailSent ? null : "Không gửi được Gmail.";
                    notification.UpdatedAt = UtcNow;
                    await _staffNotifications.SaveChangesAsync();
                }

                await PublishChangedSafeAsync(challenge, notification, "DeliveryUpdated");

                var msg = emailSent
                    ? "OTP mới đã được gửi đến email người duyệt. Mã cũ không còn hiệu lực."
                    : "OTP mới đã được tạo và có thể xem trong thông báo nội bộ; kênh email gửi thất bại.";
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

        public async Task<ServiceResult<OtpChallengeResponseDto>> ResendOtpAsync(
            OtpResendDto request,
            int requestedByStaffId,
            int storeId)
        {
            var challenge = request == null
                ? null
                : await _repository.GetByPublicIdAsync(request.OtpChallengePublicId);
            if (challenge == null
                || challenge.RequestedByStaffId != requestedByStaffId
                || challenge.StoreId != storeId)
            {
                return ServiceResult<OtpChallengeResponseDto>.Failure(
                    "Yêu cầu OTP không hợp lệ hoặc không thuộc phiên hiện tại.",
                    errorCode: OtpConstants.ErrorCodes.ContextMismatch);
            }

            if (!await IsApproverStillEligibleForChallengeAsync(challenge))
            {
                return ServiceResult<OtpChallengeResponseDto>.Failure(
                    "Người duyệt không còn quyền hoặc không còn thuộc phạm vi cửa hàng.",
                    errorCode: OtpConstants.ErrorCodes.ApproverNoLongerEligible);
            }
            return await ResendOtpAsync(request);
        }

        public async Task<ServiceResult<OtpChallengeResponseDto>> CancelTerminalRegistrationOtpAsync(
            OtpCancelDto request,
            int requestedByStaffId,
            int storeId)
        {
            if (request == null || request.OtpChallengePublicId == Guid.Empty)
                return ServiceResult<OtpChallengeResponseDto>.Failure("Thiếu mã yêu cầu OTP.");

            var nowUtc = UtcNow;
            StaffNotification? notification = null;
            try
            {
                await _repository.BeginTransactionAsync();
                var challenge = await _repository.GetByPublicIdForUpdateAsync(request.OtpChallengePublicId);
                if (challenge == null
                    || challenge.RequestedByStaffId != requestedByStaffId
                    || challenge.StoreId != storeId
                    || challenge.ActionType != OtpConstants.ActionTypes.RegisterTerminal)
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<OtpChallengeResponseDto>.Failure(
                        "Yêu cầu đăng ký Terminal không thuộc nhân viên/cửa hàng hiện tại.",
                        errorCode: OtpConstants.ErrorCodes.ContextMismatch);
                }

                if (challenge.Status == OtpConstants.Statuses.Cancelled)
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<OtpChallengeResponseDto>.Success(
                        MapResponse(challenge, nowUtc), "Yêu cầu đã được hủy trước đó.");
                }
                if (challenge.Status != OtpConstants.Statuses.Pending)
                {
                    await _repository.RollbackTransactionAsync();
                    return Failure("Yêu cầu không còn ở trạng thái có thể hủy.", challenge, nowUtc);
                }

                challenge.Status = OtpConstants.Statuses.Cancelled;
                challenge.CancelledAt = nowUtc;
                challenge.ProtectedOtpPayload = null;
                if (_staffNotifications != null)
                {
                    notification = await _staffNotifications.GetByDeduplicationKeyAsync(
                        OtpNotificationKey(challenge.PublicId));
                    if (notification != null)
                    {
                        notification.ResolvedAt ??= nowUtc;
                        notification.UpdatedAt = nowUtc;
                        notification.MeaningfulVersion = OtpConstants.Statuses.Cancelled;
                    }
                }
                await _repository.SaveChangesAsync();
                if (notification != null && _staffNotifications != null)
                    await _staffNotifications.SaveChangesAsync();
                if (_audit != null)
                    await _audit.WriteOtpAsync(
                        "TERMINAL_REGISTRATION_CANCELLED",
                        challenge.OtpChallengeId,
                        requestedByStaffId,
                        new { challenge.PublicId, challenge.StoreId, challenge.TerminalId });
                await _repository.CommitTransactionAsync();
                await PublishChangedSafeAsync(challenge, notification, "Cancelled");
                return ServiceResult<OtpChallengeResponseDto>.Success(
                    MapResponse(challenge, nowUtc), "Đã hủy yêu cầu đăng ký Terminal.");
            }
            catch (DbUpdateConcurrencyException)
            {
                await _repository.RollbackTransactionAsync();
                return ServiceResult<OtpChallengeResponseDto>.Failure(
                    "Yêu cầu đang được xử lý ở nơi khác.",
                    errorCode: WorkShiftErrorCodes.ConcurrencyConflict);
            }
            catch
            {
                await _repository.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<ServiceResult<OtpChallengeResponseDto>> CancelOpenPosOtpAsync(
            OtpCancelDto request,
            int requestedByStaffId,
            int storeId)
        {
            if (request == null || request.OtpChallengePublicId == Guid.Empty)
                return ServiceResult<OtpChallengeResponseDto>.Failure("Thiếu mã yêu cầu OTP.");

            var nowUtc = UtcNow;
            StaffNotification? notification = null;
            try
            {
                await _repository.BeginTransactionAsync();
                var challenge = await _repository.GetByPublicIdForUpdateAsync(request.OtpChallengePublicId);
                var isOpenIntent = challenge?.ActionType is OtpConstants.ActionTypes.OpenShiftLate
                    or OtpConstants.ActionTypes.OpenShiftEarly
                    or OtpConstants.ActionTypes.OpenShiftOutsideSchedule;
                if (challenge == null || !isOpenIntent
                    || challenge.RequestedByStaffId != requestedByStaffId
                    || challenge.StoreId != storeId)
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<OtpChallengeResponseDto>.Failure(
                        "Yêu cầu OTP mở ca không thuộc nhân viên/cửa hàng hiện tại.",
                        errorCode: OtpConstants.ErrorCodes.ContextMismatch);
                }

                if (challenge.Status == OtpConstants.Statuses.Cancelled)
                {
                    await _repository.RollbackTransactionAsync();
                    return ServiceResult<OtpChallengeResponseDto>.Success(
                        MapResponse(challenge, nowUtc), "Yêu cầu mở ca đã được hủy trước đó.");
                }
                if (challenge.Status is not OtpConstants.Statuses.Pending
                    and not OtpConstants.Statuses.Approved)
                {
                    await _repository.RollbackTransactionAsync();
                    return Failure("OTP đã được dùng hoặc không còn ở trạng thái có thể hủy.", challenge, nowUtc);
                }

                challenge.Status = OtpConstants.Statuses.Cancelled;
                challenge.CancelledAt = nowUtc;
                challenge.ProtectedOtpPayload = null;
                if (_staffNotifications != null)
                {
                    notification = await _staffNotifications.GetByDeduplicationKeyAsync(
                        OtpNotificationKey(challenge.PublicId));
                    if (notification != null)
                    {
                        notification.ResolvedAt ??= nowUtc;
                        notification.UpdatedAt = nowUtc;
                        notification.MeaningfulVersion = OtpConstants.Statuses.Cancelled;
                    }
                }
                await _repository.SaveChangesAsync();
                if (notification != null && _staffNotifications != null)
                    await _staffNotifications.SaveChangesAsync();
                if (_audit != null)
                    await _audit.WriteOtpAsync("OPEN_POS_INTENT_CANCELLED", challenge.OtpChallengeId,
                        requestedByStaffId,
                        new { challenge.PublicId, challenge.StoreId, challenge.TerminalId, challenge.RequestKey });
                await _repository.CommitTransactionAsync();
                await PublishChangedSafeAsync(challenge, notification, "Cancelled");
                return ServiceResult<OtpChallengeResponseDto>.Success(
                    MapResponse(challenge, nowUtc), "Đã hủy yêu cầu OTP mở ca.");
            }
            catch (DbUpdateConcurrencyException)
            {
                await _repository.RollbackTransactionAsync();
                return ServiceResult<OtpChallengeResponseDto>.Failure(
                    "Yêu cầu đang được xử lý ở nơi khác.",
                    errorCode: WorkShiftErrorCodes.ConcurrencyConflict);
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

            if (actionType == OtpConstants.ActionTypes.OpenShiftLate
                || actionType == OtpConstants.ActionTypes.OpenShiftEarly
                || actionType == OtpConstants.ActionTypes.OpenShiftOutsideSchedule)
            {
                // No WorkShift yet — target is the actor staff id.
                var scheduled = actionType == OtpConstants.ActionTypes.OpenShiftOutsideSchedule
                    ? "none"
                    : await ResolveScheduledStartCanonicalAsync(requestedByStaffId, storeId);
                var reason = request.Reason;
                var fingerprint = _fingerprint.BuildOpenShiftBoundFingerprint(
                    storeId,
                    requestedByStaffId,
                    request.StartingCash,
                    reason,
                    scheduled,
                    actionType,
                    request.TerminalId,
                    request.RequestKey);

                return (null, fingerprint, requestedByStaffId, null, $"Staff #{requestedByStaffId}");
            }

            if (actionType == OtpConstants.ActionTypes.RegisterTerminal)
            {
                var terminalId = request.TerminalId?.Trim();
                var terminalName = request.TerminalName?.Trim();
                if (string.IsNullOrWhiteSpace(terminalId) || string.IsNullOrWhiteSpace(terminalName))
                    return ("Thiếu thông tin terminal cần phê duyệt.", null, null, null, null);
                var fingerprint = _fingerprint.BuildOpenShiftBoundFingerprint(
                    storeId,
                    requestedByStaffId,
                    0,
                    terminalName,
                    $"terminal:{terminalId}|request:{request.RequestKey?.Trim()}",
                    actionType,
                    terminalId,
                    request.RequestKey);
                return (null, fingerprint, requestedByStaffId, null, $"Terminal {terminalName}");
            }

            return ("ActionType không hỗ trợ.", null, null, null, null);
        }

        private async Task<string> ResolveScheduledStartCanonicalAsync(int staffId, int storeId)
        {
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(UtcNow, _workShiftOptions.ResolveTimeZone());
            var staffShift = await _workShiftRepository.GetEffectiveStaffShiftAsync(staffId, storeId, nowLocal);
            if (staffShift?.Shift == null)
                return "none";

            var start = ScheduleIntervalResolver.Resolve(staffShift).StartLocal;
            return start.ToString("yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        private static string OtpNotificationKey(Guid publicId) =>
            $"OTP:{publicId:N}";

        private static string TerminalRejectionNotificationKey(Guid publicId, int recipientStaffId) =>
            $"OTP:{publicId:N}:REJECT:{recipientStaffId}";

        private static StaffNotification BuildOtpNotification(
            OtpChallenge challenge,
            string requesterName,
            string storeName,
            string actionLabel,
            DateTime nowUtc) => new()
        {
            StoreId = challenge.StoreId,
            RecipientStaffId = challenge.ApproverStaffId,
            Type = StaffNotificationTypes.OperationalOtpRequest,
            Title = $"Yêu cầu OTP: {actionLabel}",
            Body = $"{requesterName} yêu cầu {actionLabel} tại {storeName}. Lý do: {challenge.Reason}. " +
                   "Chi tiết thời hạn và trạng thái được hiển thị trong thẻ OTP bên dưới.",
            Severity = "WARNING",
            DeduplicationKey = OtpNotificationKey(challenge.PublicId),
            MeaningfulVersion = challenge.ExpiresAt.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            EntityType = StaffNotificationEntityTypes.OtpChallenge,
            EntityId = challenge.OtpChallengeId,
            OtpChallengeId = challenge.OtpChallengeId,
            IsRead = false,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
            EmailAttempted = false,
            EmailSent = false
        };

        private async Task<IReadOnlyList<StaffNotification>> BuildTerminalRejectionNotificationsAsync(
            OtpChallenge challenge,
            string requesterName,
            string storeName,
            int selectedApproverStaffId,
            DateTime nowUtc)
        {
            if (_permissions == null || _staffNotifications == null)
                return Array.Empty<StaffNotification>();

            var notifications = new List<StaffNotification>();
            foreach (var candidate in await _repository.GetOtpApproverCandidatesAsync(challenge.RequestedByStaffId))
            {
                if (candidate.StaffId == selectedApproverStaffId || candidate.AccountId <= 0)
                    continue;

                var permission = await _permissions.HasPermissionAsync(
                    candidate.AccountId,
                    PermissionConstants.PosWorkShiftRejectTerminal,
                    challenge.StoreId);
                if (!permission.IsSuccess || permission.Data?.Allowed != true)
                    continue;

                notifications.Add(new StaffNotification
                {
                    StoreId = challenge.StoreId,
                    RecipientStaffId = candidate.StaffId,
                    Type = StaffNotificationTypes.OperationalOtpRequest,
                    Title = "Yêu cầu đăng ký Terminal đang chờ phê duyệt",
                    Body = $"{requesterName} yêu cầu đăng ký Terminal tại {storeName}. " +
                           "Chủ doanh nghiệp có thể từ chối yêu cầu này nếu thiết bị không hợp lệ.",
                    Severity = "WARNING",
                    DeduplicationKey = TerminalRejectionNotificationKey(challenge.PublicId, candidate.StaffId),
                    MeaningfulVersion = challenge.ExpiresAt.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    EntityType = StaffNotificationEntityTypes.OtpChallenge,
                    EntityId = challenge.OtpChallengeId,
                    OtpChallengeId = challenge.OtpChallengeId,
                    IsRead = false,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc,
                    EmailAttempted = false,
                    EmailSent = false
                });
            }

            return notifications;
        }

        private async Task ResolveFailedDeliveryAsync(
            StaffNotification? notification,
            string safeError)
        {
            if (notification == null || _staffNotifications == null)
                return;

            notification.EmailAttempted = true;
            notification.EmailSent = false;
            notification.EmailErrorSummary = safeError;
            notification.UpdatedAt = UtcNow;
            await _staffNotifications.SaveChangesAsync();
        }

        private async Task ResolveOtpNotificationAsync(OtpChallenge challenge, string changeKind)
        {
            if (_staffNotifications == null)
                return;

            var notifications = await _staffNotifications.GetActiveByEntityAsync(
                challenge.StoreId,
                StaffNotificationTypes.OperationalOtpRequest,
                StaffNotificationEntityTypes.OtpChallenge,
                challenge.OtpChallengeId) ?? new List<StaffNotification>();
            if (notifications.Count == 0)
            {
                var legacyNotification = await _staffNotifications.GetByDeduplicationKeyAsync(
                    OtpNotificationKey(challenge.PublicId));
                if (legacyNotification != null && legacyNotification.ResolvedAt == null)
                    notifications.Add(legacyNotification);
            }
            if (notifications.Count == 0)
                return;

            foreach (var notification in notifications)
            {
                notification.ResolvedAt ??= UtcNow;
                notification.UpdatedAt = UtcNow;
            }
            await _staffNotifications.SaveChangesAsync();
            await PublishChangedSafeAsync(challenge, notifications, changeKind);
        }

        private Task PublishChangedSafeAsync(
            OtpChallenge challenge,
            StaffNotification? notification,
            string changeKind) =>
            PublishChangedSafeAsync(
                challenge,
                notification == null ? Array.Empty<StaffNotification>() : new[] { notification },
                changeKind);

        private async Task PublishChangedSafeAsync(
            OtpChallenge challenge,
            IReadOnlyCollection<StaffNotification> notifications,
            string changeKind)
        {
            if (_otpNotificationPublisher == null)
                return;

            try
            {
                foreach (var notification in notifications)
                    await _otpNotificationPublisher.PublishChangedAsync(
                        notification.RecipientStaffId,
                        new OperationalOtpNotificationChangedDto(
                            Guid.NewGuid().ToString("N"),
                            notification.StaffNotificationId,
                            changeKind,
                            UtcDateTime.Normalize(UtcNow)));
                if (challenge.ActionType == OtpConstants.ActionTypes.RegisterTerminal)
                    await _otpNotificationPublisher.PublishTerminalRegistrationChangedAsync(
                        challenge.RequestedByStaffId,
                        new TerminalRegistrationChangedDto(
                            challenge.PublicId,
                            challenge.Status,
                            challenge.TerminalId,
                            UtcDateTime.Normalize(challenge.ExpiresAt),
                            UtcDateTime.Normalize(UtcNow)));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OTP_NOTIFICATION_REFRESH_FAILED | ApproverStaffId={ApproverStaffId}", challenge.ApproverStaffId);
            }
        }

        private async Task WriteOtpAuditSafeAsync(string action, OtpChallenge challenge, object data)
        {
            if (_audit == null)
                return;

            try
            {
                await _audit.WriteOtpAsync(
                    action,
                    challenge.OtpChallengeId,
                    challenge.RequestedByStaffId,
                    data);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "OTP_AUDIT_WRITE_FAILED | Action={Action} ChallengeId={ChallengeId}",
                    action,
                    challenge.OtpChallengeId);
            }
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
            if (string.Equals(value, OtpConstants.ActionTypes.OpenShiftEarly, StringComparison.OrdinalIgnoreCase))
                return OtpConstants.ActionTypes.OpenShiftEarly;
            if (string.Equals(value, OtpConstants.ActionTypes.OpenShiftOutsideSchedule, StringComparison.OrdinalIgnoreCase))
                return OtpConstants.ActionTypes.OpenShiftOutsideSchedule;
            if (string.Equals(value, OtpConstants.ActionTypes.RegisterTerminal, StringComparison.OrdinalIgnoreCase))
                return OtpConstants.ActionTypes.RegisterTerminal;
            return null;
        }

        private static string ResolveActionLabel(string actionType)
        {
            return actionType switch
            {
                OtpConstants.ActionTypes.CashDifference => "Xác nhận đóng ca có chênh lệch",
                OtpConstants.ActionTypes.CloseShiftException => "Xác nhận đóng ca ngoại lệ",
                OtpConstants.ActionTypes.OpenShiftLate => "Xác nhận mở ca trễ",
                OtpConstants.ActionTypes.OpenShiftEarly => "Xác nhận mở ca sớm",
                OtpConstants.ActionTypes.OpenShiftOutsideSchedule => "Xác nhận mở POS ngoài lịch",
                OtpConstants.ActionTypes.RegisterTerminal => "Xác nhận đăng ký terminal POS",
                _ => "Xác nhận OTP"
            };
        }

        private static string? EnsurePendingChallenge(OtpChallenge challenge, DateTime nowUtc)
        {
            if (challenge.Status == OtpConstants.Statuses.Pending && challenge.ExpiresAt <= nowUtc)
            {
                challenge.Status = OtpConstants.Statuses.Expired;
                challenge.ProtectedOtpPayload = null;
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
                challenge.ProtectedOtpPayload = null;
                return "Yêu cầu OTP đã bị khóa do nhập sai quá số lần cho phép.";
            }

            return null;
        }

        private static bool IsSmtpPasswordNotConfigured(Exception ex)
        {
            var text = ex.Message ?? string.Empty;
            return text.Contains(
                       OtpConstants.ErrorCodes.EmailSmtpPasswordNotConfigured,
                       StringComparison.OrdinalIgnoreCase)
                   || text.Contains("Thiếu Email:Password", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("Thiếu Gmail App Password", StringComparison.OrdinalIgnoreCase);
        }

        private static ServiceResult<OtpChallengeResponseDto> Failure(
            string message,
            OtpChallenge challenge,
            DateTime nowUtc,
            string? errorCode = null)
        {
            errorCode ??= challenge.Status switch
            {
                OtpConstants.Statuses.Expired => OtpConstants.ErrorCodes.Expired,
                OtpConstants.Statuses.Locked => OtpConstants.ErrorCodes.VerificationLocked,
                OtpConstants.Statuses.Approved or OtpConstants.Statuses.Used => OtpConstants.ErrorCodes.AlreadyUsed,
                _ => null
            };
            return new ServiceResult<OtpChallengeResponseDto>
            {
                IsSuccess = false,
                Message = message,
                ErrorCode = errorCode,
                Data = MapResponse(challenge, nowUtc)
            };
        }

        private static OtpChallengeResponseDto MapResponse(
            OtpChallenge challenge,
            DateTime nowUtc,
            bool wasExistingActive = false)
        {
            var expiresInSeconds = Math.Max(0, (int)Math.Ceiling((challenge.ExpiresAt - nowUtc).TotalSeconds));
            OperationalOtpChallengePolicy.CanResend(challenge, nowUtc, out var resendAvailableInSeconds);
            var lockedUntilUtc = OperationalOtpChallengePolicy.GetLockedUntilUtc(challenge);
            var retryAfter = OperationalOtpChallengePolicy.GetRetryAfterSeconds(challenge, nowUtc);
            var remainingAttempts = Math.Max(0, OtpConstants.MaxFailedAttempts - challenge.FailedAttempts);

            return new OtpChallengeResponseDto
            {
                HasActiveChallenge = true,
                OtpChallengePublicId = challenge.PublicId,
                Status = challenge.Status,
                ActionType = challenge.ActionType,
                OpenContext = challenge.ActionType == OtpConstants.ActionTypes.OpenShiftOutsideSchedule
                    ? WorkShiftOpenContexts.OutsideSchedule
                    : challenge.ActionType == OtpConstants.ActionTypes.OpenShiftEarly
                        ? WorkShiftOpenContexts.EarlyForSchedule
                    : challenge.ActionType == OtpConstants.ActionTypes.OpenShiftLate
                        ? WorkShiftOpenContexts.LateForSchedule
                        : null,
                TerminalId = challenge.TerminalId,
                TerminalName = challenge.TerminalName,
                Reason = challenge.Reason,
                RequestKey = challenge.RequestKey,
                ExpiresInSeconds = expiresInSeconds,
                ResendAvailableInSeconds = resendAvailableInSeconds,
                RemainingAttempts = remainingAttempts,
                Locked = challenge.Status == OtpConstants.Statuses.Locked,
                LockedUntilUtc = lockedUntilUtc,
                RetryAfter = retryAfter,
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
            if (roleNames.Contains(RoleConstants.AreaManager))
                return RoleConstants.AreaManager;
            if (roleNames.Contains(RoleConstants.BusinessOwner))
                return RoleConstants.BusinessOwner;
            return "người duyệt";
        }

        private async Task<Staff?> ResolvePermissionApproverAsync(
            string actionType,
            int storeId,
            int requestedByStaffId)
        {
            if (!OperationalOtpAuthorization.TryGetApproverPermission(actionType, out var permissionCode))
                return null;

            foreach (var candidate in await _repository.GetOtpApproverCandidatesAsync(requestedByStaffId))
            {
                if (candidate.AccountId <= 0) continue;
                var decision = await _permissions!.HasPermissionAsync(candidate.AccountId, permissionCode, storeId);
                if (decision.IsSuccess && decision.Data?.Allowed == true)
                    return candidate;
            }
            return null;
        }

        private async Task<bool> IsApproverStillEligibleForChallengeAsync(OtpChallenge challenge)
        {
            if (challenge.ApproverStaffId == challenge.RequestedByStaffId)
                return false;

            if (_permissions == null)
            {
                return await _repository.IsApproverStillEligibleAsync(
                    challenge.ApproverStaffId,
                    challenge.StoreId,
                    challenge.RequestedByStaffId);
            }

            var accountId = challenge.ApproverStaff?.AccountId ?? 0;
            if (accountId <= 0)
                return false;

            if (!OperationalOtpAuthorization.TryGetApproverPermission(
                    challenge.ActionType, out var permissionCode))
                return false;

            var decision = await _permissions.HasPermissionAsync(
                accountId,
                permissionCode,
                challenge.StoreId);
            return decision.IsSuccess && decision.Data?.Allowed == true;
        }

        private static string? NormalizeSecurityHash(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var normalized = value.Trim().ToUpperInvariant();
            return normalized.Length == 64 && normalized.All(Uri.IsHexDigit)
                ? normalized
                : null;
        }
    }
}
