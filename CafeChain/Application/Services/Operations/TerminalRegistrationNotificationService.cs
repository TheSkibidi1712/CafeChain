using CafeChain.Application.Authorization;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Operations;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Application.Tools;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Operations;

namespace CafeChain.Application.Services.Operations;

public sealed class TerminalRegistrationNotificationService : ITerminalRegistrationNotificationService
{
    private readonly IStaffNotificationRepository _notifications;
    private readonly IOtpProtectedPayloadService _protectedPayload;
    private readonly IWorkShiftService _workShifts;
    private readonly IAdminPermissionService _permissions;
    private readonly TimeProvider _timeProvider;

    public TerminalRegistrationNotificationService(
        IStaffNotificationRepository notifications,
        IOtpProtectedPayloadService protectedPayload,
        IWorkShiftService workShifts,
        IAdminPermissionService permissions,
        TimeProvider? timeProvider = null)
    {
        _notifications = notifications;
        _protectedPayload = protectedPayload;
        _workShifts = workShifts;
        _permissions = permissions;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<ServiceResult<OtpRevealResultDto>> RevealOtpAsync(
        int approverStaffId,
        int notificationId,
        IReadOnlyCollection<int>? allowedStoreIds = null) =>
        RevealOperationalOtpAsync(approverStaffId, notificationId, allowedStoreIds);

    public async Task<ServiceResult<OtpRevealResultDto>> RevealOperationalOtpAsync(
        int approverStaffId,
        int notificationId,
        IReadOnlyCollection<int>? allowedStoreIds = null)
    {
        var context = await ResolveAsync(approverStaffId, notificationId, allowedStoreIds);
        if (!context.IsSuccess || context.Data == null)
            return ServiceResult<OtpRevealResultDto>.Failure(
                context.Message, errorCode: context.ErrorCode);

        var challenge = context.Data;
        var nowUtc = UtcDateTime.Normalize(_timeProvider.GetUtcNow().UtcDateTime);
        if (UtcDateTime.Normalize(challenge.ExpiresAt) <= nowUtc
            || challenge.Status == OtpConstants.Statuses.Expired)
        {
            return ServiceResult<OtpRevealResultDto>.Failure(
                "OTP đã hết hạn. Vui lòng gửi yêu cầu mới.",
                errorCode: OtpConstants.ErrorCodes.Expired);
        }
        if (challenge.Status == OtpConstants.Statuses.Used)
            return ServiceResult<OtpRevealResultDto>.Failure(
                "OTP đã được sử dụng.", errorCode: OtpConstants.ErrorCodes.AlreadyUsed);
        if (challenge.Status is OtpConstants.Statuses.Cancelled or OtpConstants.Statuses.Locked)
            return ServiceResult<OtpRevealResultDto>.Failure(
                "OTP đã bị hủy hoặc khóa.", errorCode: OtpConstants.ErrorCodes.Cancelled);

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
            ExpiresAtUtc = UtcDateTime.Normalize(challenge.ExpiresAt),
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

        var context = await ResolveAsync(
            approverStaffId,
            notificationId,
            allowedStoreIds,
            OtpConstants.ActionTypes.RegisterTerminal);
        if (!context.IsSuccess || context.Data == null)
            return ServiceResult.Failure(context.Message, errorCode: context.ErrorCode);

        return await _workShifts.ConfirmTerminalRegistrationAsync(
            approverStaffId,
            context.Data.StoreId,
            context.Data.PublicId,
            request.OtpCode,
            request.RequestKey);
    }

    private async Task<ServiceResult<OtpChallenge>> ResolveAsync(
        int approverStaffId,
        int notificationId,
        IReadOnlyCollection<int>? allowedStoreIds,
        string? requiredActionType = null)
    {
        if (approverStaffId <= 0 || notificationId <= 0)
            return ServiceResult<OtpChallenge>.Failure("Notification không hợp lệ.");

        var notification = await _notifications.GetAsync(
            approverStaffId, notificationId, allowedStoreIds, tracking: false);
        if (notification == null
            || notification.Type != StaffNotificationTypes.OperationalOtpRequest
            || notification.EntityType != StaffNotificationEntityTypes.OtpChallenge)
        {
            return ServiceResult<OtpChallenge>.Failure(
                "Không tìm thấy thông báo hoặc bạn không có quyền truy cập.",
                errorCode: OtpConstants.ErrorCodes.ContextMismatch);
        }

        var challengeId = notification.OtpChallengeId ?? notification.EntityId;
        var challenges = await _notifications.GetOtpChallengesAsync(
            approverStaffId, new[] { challengeId });
        var challenge = challenges.SingleOrDefault();
        if (challenge == null
            || challenge.OtpChallengeId != challengeId
            || challenge.StoreId != notification.StoreId
            || challenge.ApproverStaffId != approverStaffId)
        {
            return ServiceResult<OtpChallenge>.Failure(
                "Thông báo không khớp yêu cầu OTP vận hành.",
                errorCode: OtpConstants.ErrorCodes.ContextMismatch);
        }

        if (requiredActionType != null && challenge.ActionType != requiredActionType)
        {
            return ServiceResult<OtpChallenge>.Failure(
                "Thông báo không thuộc yêu cầu xác nhận Terminal.",
                errorCode: OtpConstants.ErrorCodes.ContextMismatch);
        }

        if (challenge.ApproverStaff == null
            || !challenge.ApproverStaff.Active
            || challenge.ApproverStaff.Account == null
            || !challenge.ApproverStaff.Account.Active
            || !OperationalOtpAuthorization.TryGetApproverPermission(
                challenge.ActionType, out var permissionCode))
        {
            return ServiceResult<OtpChallenge>.Failure(
                "Yêu cầu OTP không hợp lệ hoặc người xác nhận không còn hoạt động.",
                errorCode: OtpConstants.ErrorCodes.ApproverNoLongerEligible);
        }

        var permission = await _permissions.HasPermissionAsync(
            challenge.ApproverStaff.AccountId,
            permissionCode,
            challenge.StoreId);
        if (!permission.IsSuccess || permission.Data?.Allowed != true)
        {
            return ServiceResult<OtpChallenge>.Failure(
                "Bạn không có quyền xác nhận loại yêu cầu OTP này trong chi nhánh hiện tại.",
                errorCode: OtpConstants.ErrorCodes.ApproverNoLongerEligible);
        }

        return ServiceResult<OtpChallenge>.Success(challenge);
    }
}
