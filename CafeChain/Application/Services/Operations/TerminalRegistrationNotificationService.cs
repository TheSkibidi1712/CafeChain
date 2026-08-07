using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Operations;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Infrastructure.Interfaces.Operations;

namespace CafeChain.Application.Services.Operations;

public sealed class TerminalRegistrationNotificationService : ITerminalRegistrationNotificationService
{
    private readonly IStaffNotificationRepository _notifications;
    private readonly IOtpChallengeRepository _otpChallenges;
    private readonly IOtpProtectedPayloadService _protectedPayload;
    private readonly IWorkShiftService _workShifts;
    private readonly TimeProvider _timeProvider;

    public TerminalRegistrationNotificationService(
        IStaffNotificationRepository notifications,
        IOtpChallengeRepository otpChallenges,
        IOtpProtectedPayloadService protectedPayload,
        IWorkShiftService workShifts,
        TimeProvider? timeProvider = null)
    {
        _notifications = notifications;
        _otpChallenges = otpChallenges;
        _protectedPayload = protectedPayload;
        _workShifts = workShifts;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ServiceResult<OtpRevealResultDto>> RevealOtpAsync(
        int approverStaffId,
        int notificationId,
        IReadOnlyCollection<int>? allowedStoreIds = null)
    {
        var context = await ResolveAsync(approverStaffId, notificationId, allowedStoreIds);
        if (!context.IsSuccess || context.Data == null)
            return ServiceResult<OtpRevealResultDto>.Failure(context.Message, errorCode: context.ErrorCode);

        var challenge = context.Data;
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        if (challenge.ExpiresAt <= nowUtc || challenge.Status == OtpConstants.Statuses.Expired)
            return ServiceResult<OtpRevealResultDto>.Failure(
                "OTP đã hết hạn. Vui lòng gửi yêu cầu mới.",
                errorCode: OtpConstants.ErrorCodes.Expired);
        if (challenge.Status == OtpConstants.Statuses.Used)
            return ServiceResult<OtpRevealResultDto>.Failure("OTP đã được sử dụng.", errorCode: OtpConstants.ErrorCodes.AlreadyUsed);
        if (challenge.Status is OtpConstants.Statuses.Cancelled or OtpConstants.Statuses.Locked)
            return ServiceResult<OtpRevealResultDto>.Failure("OTP đã bị hủy.", errorCode: OtpConstants.ErrorCodes.Cancelled);
        if (challenge.Status != OtpConstants.Statuses.Pending
            || !_protectedPayload.TryUnprotect(
                challenge.ProtectedOtpPayload,
                challenge.PublicId,
                approverStaffId,
                challenge.ExpiresAt,
                nowUtc,
                out var code))
        {
            return ServiceResult<OtpRevealResultDto>.Failure(
                "Không thể xem OTP ở trạng thái hiện tại.",
                errorCode: OtpConstants.ErrorCodes.Invalid);
        }

        return ServiceResult<OtpRevealResultDto>.Success(new OtpRevealResultDto
        {
            Code = code,
            ExpiresAtUtc = challenge.ExpiresAt,
            ServerNowUtc = nowUtc
        });
    }

    public async Task<ServiceResult> ConfirmAsync(
        int approverStaffId,
        int notificationId,
        ConfirmTerminalNotificationRequestDto request,
        IReadOnlyCollection<int>? allowedStoreIds = null)
    {
        if (request == null)
            return ServiceResult.Failure("Thiếu dữ liệu xác nhận Terminal.");
        var context = await ResolveAsync(approverStaffId, notificationId, allowedStoreIds);
        if (!context.IsSuccess || context.Data == null)
            return ServiceResult.Failure(context.Message, errorCode: context.ErrorCode);

        return await _workShifts.ConfirmTerminalRegistrationAsync(
            approverStaffId,
            context.Data.StoreId,
            context.Data.PublicId,
            request.OtpCode,
            request.RequestKey);
    }

    private async Task<ServiceResult<CafeChain.Models.Operations.OtpChallenge>> ResolveAsync(
        int approverStaffId,
        int notificationId,
        IReadOnlyCollection<int>? allowedStoreIds)
    {
        if (approverStaffId <= 0 || notificationId <= 0)
            return ServiceResult<CafeChain.Models.Operations.OtpChallenge>.Failure("Notification không hợp lệ.");
        var notification = await _notifications.GetAsync(
            approverStaffId, notificationId, allowedStoreIds, tracking: false);
        if (notification == null
            || notification.Type != StaffNotificationTypes.OperationalOtpRequest)
        {
            return ServiceResult<CafeChain.Models.Operations.OtpChallenge>.Failure(
                "Không tìm thấy thông báo hoặc bạn không có quyền truy cập.",
                errorCode: OtpConstants.ErrorCodes.ContextMismatch);
        }

        var challengeId = notification.OtpChallengeId ?? notification.EntityId;
        var challenges = await _notifications.GetOtpChallengesAsync(
            approverStaffId, new[] { challengeId });
        var challenge = challenges.SingleOrDefault();
        if (challenge == null
            || challenge.StoreId != notification.StoreId
            || challenge.ActionType != OtpConstants.ActionTypes.RegisterTerminal)
        {
            return ServiceResult<CafeChain.Models.Operations.OtpChallenge>.Failure(
                "Thông báo không thuộc yêu cầu đăng ký Terminal.",
                errorCode: OtpConstants.ErrorCodes.ContextMismatch);
        }

        return ServiceResult<CafeChain.Models.Operations.OtpChallenge>.Success(challenge);
    }
}
