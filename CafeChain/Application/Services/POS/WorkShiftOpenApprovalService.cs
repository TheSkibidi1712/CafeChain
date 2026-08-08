using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Options;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Operations;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace CafeChain.Application.Services.POS;

public sealed class WorkShiftOpenApprovalService : IWorkShiftOpenApprovalService
{
    private readonly IWorkShiftOpenApprovalRepository _repository;
    private readonly IWorkShiftService _workShifts;
    private readonly IOtpChallengeRepository _staffLookup;
    private readonly IAdminPermissionService? _permissions;
    private readonly IStaffNotificationRepository? _notifications;
    private readonly IWorkShiftAuditService? _audit;
    private readonly IWorkShiftOpenApprovalPublisher? _publisher;
    private readonly WorkShiftOptions _options;
    private readonly TimeProvider _timeProvider;

    public WorkShiftOpenApprovalService(
        IWorkShiftOpenApprovalRepository repository,
        IWorkShiftService workShifts,
        IOtpChallengeRepository staffLookup,
        IOptions<WorkShiftOptions>? options = null,
        IAdminPermissionService? permissions = null,
        IStaffNotificationRepository? notifications = null,
        IWorkShiftAuditService? audit = null,
        IWorkShiftOpenApprovalPublisher? publisher = null,
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _workShifts = workShifts;
        _staffLookup = staffLookup;
        _permissions = permissions;
        _notifications = notifications;
        _audit = audit;
        _publisher = publisher;
        _options = options?.Value ?? new WorkShiftOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ServiceResult<WorkShiftOpenApprovalDto>> CreateAsync(
        int requesterStaffId, int storeId, CreateWorkShiftOpenApprovalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.TerminalId)
            || string.IsNullOrWhiteSpace(request.RequestKey)
            || !IsValidReason(request.Reason))
            return ServiceResult<WorkShiftOpenApprovalDto>.Failure("Terminal, RequestKey và lý do 10–500 ký tự là bắt buộc.");

        var assessment = await _workShifts.AssessOpenContextAsync(
            requesterStaffId, storeId, request.TerminalId.Trim(), cancellationToken);
        if (!assessment.IsSuccess || assessment.Data == null)
            return ServiceResult<WorkShiftOpenApprovalDto>.Failure(assessment.Message, errorCode: assessment.ErrorCode);
        if (assessment.Data.OpenContext != WorkShiftOpenContexts.LateForSchedule
            || assessment.Data.MinutesLate <= _options.LateApprovalAfterMinutes
            || !assessment.Data.SourceStaffShiftId.HasValue
            || !assessment.Data.PlannedEndUtc.HasValue)
            return ServiceResult<WorkShiftOpenApprovalDto>.Failure(
                "Chỉ tạo yêu cầu duyệt khi ca trễ trên 30 phút và lịch cũ còn trong cửa sổ hợp lệ.",
                errorCode: WorkShiftErrorCodes.LateOpenApprovalExpired);

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAtUtc = assessment.Data.PlannedEndUtc.Value.AddMinutes(_options.PostEndGraceMinutes);
        if (expiresAtUtc <= nowUtc)
            return ServiceResult<WorkShiftOpenApprovalDto>.Failure(
                "Ca làm đã kết thúc quá lâu. Chỉ có thể tạo ca ngoài lịch hoặc lịch bổ sung.",
                errorCode: WorkShiftErrorCodes.LateOpenApprovalExpired);

        await _repository.BeginTransactionAsync(cancellationToken);
        try
        {
            var existing = await _repository.GetPendingAsync(
                storeId, requesterStaffId, assessment.Data.SourceStaffShiftId.Value,
                request.TerminalId.Trim(), cancellationToken);
            if (existing != null && existing.ExpiresAtUtc > nowUtc)
            {
                await _repository.CommitTransactionAsync(cancellationToken);
                return ServiceResult<WorkShiftOpenApprovalDto>.Success(Map(existing, nowUtc),
                    "Yêu cầu duyệt mở ca đang chờ Manager xử lý.");
            }

            var approval = new WorkShiftOpenApprovalRequest
            {
                PublicId = Guid.NewGuid(),
                RequestKey = request.RequestKey.Trim(),
                StoreId = storeId,
                RequestedByStaffId = requesterStaffId,
                SourceStaffShiftId = assessment.Data.SourceStaffShiftId.Value,
                TerminalId = request.TerminalId.Trim(),
                MinutesLate = assessment.Data.MinutesLate,
                Reason = request.Reason.Trim(),
                Status = WorkShiftOpenApprovalStatuses.Pending,
                RequestedAtUtc = nowUtc,
                ExpiresAtUtc = expiresAtUtc
            };
            await _repository.AddAsync(approval, cancellationToken);

            if (_notifications != null)
            {
                var candidates = await _staffLookup.GetOtpApproverCandidatesAsync(requesterStaffId);
                foreach (var candidate in candidates.Where(x => x.AccountId > 0))
                {
                    if (_permissions != null)
                    {
                        var permission = await _permissions.HasPermissionAsync(
                            candidate.AccountId, PermissionConstants.PosWorkShiftApproveLateOpen, storeId);
                        if (!permission.IsSuccess || permission.Data?.Allowed != true) continue;
                    }
                    _notifications.Add(new StaffNotification
                    {
                        StoreId = storeId,
                        RecipientStaffId = candidate.StaffId,
                        Type = StaffNotificationTypes.LateOpenApprovalRequest,
                        Title = "Yêu cầu duyệt mở ca trễ",
                        Body = $"{approval.MinutesLate} phút trễ · {approval.Reason}",
                        Severity = "WARNING",
                        DeduplicationKey = $"LATE_OPEN:{approval.PublicId:N}:{candidate.StaffId}",
                        MeaningfulVersion = approval.Status,
                        EntityType = StaffNotificationEntityTypes.WorkShiftOpenApproval,
                        EntityId = approval.WorkShiftOpenApprovalRequestId,
                        CreatedAt = nowUtc,
                        UpdatedAt = nowUtc
                    });
                }
                await _notifications.SaveChangesAsync(cancellationToken);
            }
            await _repository.CommitTransactionAsync(cancellationToken);
            await PublishSafeAsync(approval, cancellationToken);
            if (_audit != null)
                await _audit.WriteAsync("WORKSHIFT_LATE_OPEN_APPROVAL_REQUESTED", 0, requesterStaffId,
                    null, new { approval.PublicId, approval.StoreId, approval.SourceStaffShiftId, approval.TerminalId, approval.MinutesLate, approval.Reason }, cancellationToken);
            return ServiceResult<WorkShiftOpenApprovalDto>.Success(Map(approval, nowUtc),
                "Đã gửi yêu cầu đến Store Manager. Vui lòng chờ duyệt.");
        }
        catch (DbUpdateException)
        {
            await _repository.RollbackTransactionAsync(cancellationToken);
            var existing = await _repository.GetPendingAsync(
                storeId, requesterStaffId, assessment.Data.SourceStaffShiftId.Value,
                request.TerminalId.Trim(), cancellationToken);
            return existing != null
                ? ServiceResult<WorkShiftOpenApprovalDto>.Success(Map(existing, nowUtc), "Yêu cầu đang chờ xử lý.")
                : ServiceResult<WorkShiftOpenApprovalDto>.Failure("Yêu cầu trùng hoặc dữ liệu vừa thay đổi.", errorCode: WorkShiftErrorCodes.ConcurrencyConflict);
        }
        catch
        {
            await _repository.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ServiceResult<WorkShiftOpenApprovalDto>> GetAsync(
        int actorStaffId, Guid publicId, CancellationToken cancellationToken = default)
    {
        var approval = await _repository.GetByPublicIdAsync(publicId, false, cancellationToken);
        if (approval == null || (approval.RequestedByStaffId != actorStaffId
            && !await CanApproveAsync(actorStaffId, approval.StoreId)))
            return ServiceResult<WorkShiftOpenApprovalDto>.Failure("Không tìm thấy yêu cầu hoặc không có quyền truy cập.");
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        return ServiceResult<WorkShiftOpenApprovalDto>.Success(Map(approval, nowUtc));
    }

    public async Task<ServiceResult<IReadOnlyList<WorkShiftOpenApprovalDto>>> GetPendingAsync(
        int actorStaffId,
        IReadOnlyCollection<int> allowedStoreIds,
        CancellationToken cancellationToken = default)
    {
        var authorizedStores = new List<int>();
        foreach (var storeId in allowedStoreIds.Distinct())
            if (await CanApproveAsync(actorStaffId, storeId)) authorizedStores.Add(storeId);
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var rows = await _repository.GetPendingForStoresAsync(authorizedStores, cancellationToken);
        return ServiceResult<IReadOnlyList<WorkShiftOpenApprovalDto>>.Success(
            rows.Select(x => Map(x, nowUtc)).ToArray());
    }

    public async Task<ServiceResult<WorkShiftOpenApprovalDto>> DecideAsync(
        int decisionMakerStaffId, int storeId, Guid publicId,
        DecideWorkShiftOpenApprovalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || !await CanApproveAsync(decisionMakerStaffId, storeId))
            return ServiceResult<WorkShiftOpenApprovalDto>.Failure("Bạn không có quyền duyệt mở ca trễ.", errorCode: WorkShiftErrorCodes.InvalidApproverScope);
        var decision = request.Decision?.Trim().ToUpperInvariant();
        var targetStatus = decision switch
        {
            "APPROVE" or "APPROVED" => WorkShiftOpenApprovalStatuses.Approved,
            "REJECT" or "REJECTED" => WorkShiftOpenApprovalStatuses.Rejected,
            "CONVERT" or "CONVERTED_TO_OUTSIDE_SCHEDULE" => WorkShiftOpenApprovalStatuses.ConvertedToOutsideSchedule,
            _ => null
        };
        if (targetStatus == null)
            return ServiceResult<WorkShiftOpenApprovalDto>.Failure("Quyết định không hợp lệ.");

        await _repository.BeginTransactionAsync(cancellationToken);
        try
        {
            var approval = await _repository.GetByPublicIdAsync(publicId, true, cancellationToken);
            if (approval == null || approval.StoreId != storeId)
            {
                await _repository.RollbackTransactionAsync(cancellationToken);
                return ServiceResult<WorkShiftOpenApprovalDto>.Failure("Không tìm thấy yêu cầu.");
            }
            var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
            if (approval.Status != WorkShiftOpenApprovalStatuses.Pending)
            {
                await _repository.RollbackTransactionAsync(cancellationToken);
                return ServiceResult<WorkShiftOpenApprovalDto>.Success(Map(approval, nowUtc), "Yêu cầu đã được quyết định trước đó.");
            }
            if (approval.ExpiresAtUtc <= nowUtc && targetStatus != WorkShiftOpenApprovalStatuses.ConvertedToOutsideSchedule)
            {
                approval.Status = WorkShiftOpenApprovalStatuses.Expired;
                await _repository.SaveChangesAsync(cancellationToken);
                await _repository.CommitTransactionAsync(cancellationToken);
                await PublishSafeAsync(approval, cancellationToken);
                return ServiceResult<WorkShiftOpenApprovalDto>.Failure("Lịch cũ đã hết cửa sổ mở. Chỉ có thể chuyển sang ca ngoài lịch.", errorCode: WorkShiftErrorCodes.LateOpenApprovalExpired);
            }
            if (!string.IsNullOrWhiteSpace(request.RowVersion)
                && !RowVersionMatches(request.RowVersion, approval.RowVersion))
            {
                await _repository.RollbackTransactionAsync(cancellationToken);
                return ServiceResult<WorkShiftOpenApprovalDto>.Failure("Yêu cầu vừa được thay đổi.", errorCode: WorkShiftErrorCodes.ConcurrencyConflict);
            }
            approval.Status = targetStatus;
            approval.DecidedByStaffId = decisionMakerStaffId;
            approval.DecidedAtUtc = nowUtc;
            approval.DecisionReason = request.Reason?.Trim();
            await _repository.SaveChangesAsync(cancellationToken);
            if (_notifications != null)
            {
                var active = await _notifications.GetActiveByEntityAsync(
                    storeId, StaffNotificationTypes.LateOpenApprovalRequest,
                    StaffNotificationEntityTypes.WorkShiftOpenApproval,
                    approval.WorkShiftOpenApprovalRequestId,
                    cancellationToken);
                foreach (var notification in active)
                {
                    notification.ResolvedAt = nowUtc;
                    notification.UpdatedAt = nowUtc;
                    notification.MeaningfulVersion = targetStatus;
                }
                _notifications.Add(new StaffNotification
                {
                    StoreId = storeId,
                    RecipientStaffId = approval.RequestedByStaffId,
                    Type = StaffNotificationTypes.LateOpenApprovalRequest,
                    Title = "Kết quả duyệt mở ca trễ",
                    Body = $"Trạng thái: {targetStatus}. {approval.DecisionReason}",
                    Severity = targetStatus == WorkShiftOpenApprovalStatuses.Rejected ? "ERROR" : "INFO",
                    DeduplicationKey = $"LATE_OPEN_RESULT:{approval.PublicId:N}",
                    MeaningfulVersion = targetStatus,
                    EntityType = StaffNotificationEntityTypes.WorkShiftOpenApproval,
                    EntityId = approval.WorkShiftOpenApprovalRequestId,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                });
                await _notifications.SaveChangesAsync(cancellationToken);
            }
            await _repository.CommitTransactionAsync(cancellationToken);
            await PublishSafeAsync(approval, cancellationToken);
            if (_audit != null)
                await _audit.WriteAsync("WORKSHIFT_LATE_OPEN_APPROVAL_DECIDED", 0, decisionMakerStaffId,
                    new { Status = WorkShiftOpenApprovalStatuses.Pending },
                    new { approval.PublicId, approval.Status, approval.DecisionReason, approval.DecidedAtUtc }, cancellationToken);
            return ServiceResult<WorkShiftOpenApprovalDto>.Success(Map(approval, nowUtc), "Đã cập nhật quyết định mở ca trễ.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await _repository.RollbackTransactionAsync(cancellationToken);
            return ServiceResult<WorkShiftOpenApprovalDto>.Failure("Yêu cầu đã được xử lý ở nơi khác.", errorCode: WorkShiftErrorCodes.ConcurrencyConflict);
        }
        catch
        {
            await _repository.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task<bool> CanApproveAsync(int staffId, int storeId)
    {
        var staff = await _staffLookup.GetRequestingStaffAsync(staffId, storeId);
        if (staff?.AccountId is not > 0 || !staff.Active || !staff.Account.Active) return false;
        if (_permissions == null) return false;
        var result = await _permissions.HasPermissionAsync(
            staff.AccountId, PermissionConstants.PosWorkShiftApproveLateOpen, storeId);
        return result.IsSuccess && result.Data?.Allowed == true;
    }

    private bool IsValidReason(string? reason)
    {
        var value = reason?.Trim() ?? string.Empty;
        return value.Length >= _options.MinimumReasonLength
            && value.Length <= _options.MaximumReasonLength
            && value.Any(char.IsLetterOrDigit);
    }

    private static bool RowVersionMatches(string supplied, byte[]? actual)
    {
        try
        {
            var expected = Convert.FromBase64String(supplied);
            return expected.Length > 0
                && actual is { Length: > 0 }
                && CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async Task PublishSafeAsync(WorkShiftOpenApprovalRequest approval, CancellationToken cancellationToken)
    {
        if (_publisher == null) return;
        await _publisher.PublishAsync(new WorkShiftOpenApprovalChangedDto(
            approval.PublicId, approval.StoreId, approval.RequestedByStaffId,
            approval.Status, _timeProvider.GetUtcNow().UtcDateTime), cancellationToken);
    }

    private WorkShiftOpenApprovalDto Map(WorkShiftOpenApprovalRequest x, DateTime nowUtc) => new()
    {
        PublicId = x.PublicId,
        RequestKey = x.RequestKey,
        StoreId = x.StoreId,
        RequestedByStaffId = x.RequestedByStaffId,
        RequestedByName = x.RequestedByStaff?.FullName ?? string.Empty,
        DecidedByStaffId = x.DecidedByStaffId,
        DecidedByName = x.DecidedByStaff?.FullName,
        SourceStaffShiftId = x.SourceStaffShiftId,
        TerminalId = x.TerminalId,
        MinutesLate = x.MinutesLate,
        Reason = x.Reason,
        Status = x.Status == WorkShiftOpenApprovalStatuses.Pending && x.ExpiresAtUtc <= nowUtc
            ? WorkShiftOpenApprovalStatuses.Expired : x.Status,
        DecisionReason = x.DecisionReason,
        RequestedAtUtc = x.RequestedAtUtc,
        ExpiresAtUtc = x.ExpiresAtUtc,
        DecidedAtUtc = x.DecidedAtUtc,
        ServerNowUtc = nowUtc,
        RowVersion = x.RowVersion is { Length: > 0 } ? Convert.ToBase64String(x.RowVersion) : null
    };
}
