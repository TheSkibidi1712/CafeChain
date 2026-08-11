using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Operations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Models.Stores;

namespace CafeChain.Application.Services.POS;

public sealed class PosAccessSessionService : IPosAccessSessionService
{
    private const string SessionConflictErrorCode = "POS_SESSION_CONFLICT";
    private readonly IPosAccessSessionRepository _repository;
    private readonly IPosAccessSessionPublisher? _publisher;
    private readonly IWorkShiftAuditService? _audit;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PosAccessSessionService> _logger;
    private readonly IAdminPermissionService? _permissions;
    private readonly List<(PosAccessSession Session, string? Reason)> _pendingPublications = [];

    public PosAccessSessionService(
        IPosAccessSessionRepository repository,
        IPosAccessSessionPublisher? publisher = null,
        IWorkShiftAuditService? audit = null,
        TimeProvider? timeProvider = null,
        ILogger<PosAccessSessionService>? logger = null,
        IAdminPermissionService? permissions = null)
    {
        _repository = repository;
        _publisher = publisher;
        _audit = audit;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<PosAccessSessionService>.Instance;
        _permissions = permissions;
    }

    public async Task<PosAccessSession> CreateAsync(
        int accountId, int staffId, int storeId, string terminalId,
        int exchangeContextId, int? workShiftId, DateTime expiresAtUtc,
        CancellationToken cancellationToken = default,
        bool publishAfterCommit = true)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var session = new PosAccessSession
        {
            PublicId = Guid.NewGuid(),
            JwtId = Guid.NewGuid().ToString("N"),
            AccountId = accountId,
            StaffId = staffId,
            StoreId = storeId,
            TerminalId = terminalId.Trim(),
            WorkShiftId = workShiftId,
            ExchangeContextId = exchangeContextId,
            Status = PosAccessSessionStatuses.Active,
            IssuedAtUtc = nowUtc,
            ExpiresAtUtc = expiresAtUtc
        };
        await _repository.BeginTransactionAsync(cancellationToken);
        IReadOnlyList<PosAccessSession> replacedSessions;
        try
        {
            replacedSessions = await _repository.CreateReplacingActiveAsync(session, nowUtc, cancellationToken);
            if (_audit != null)
                await _audit.WriteAsync("POS_SESSION_CREATED", workShiftId ?? 0, staffId, null,
                    new { session.PublicId, session.TerminalId, session.ExpiresAtUtc }, cancellationToken);
            await _repository.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _repository.RollbackTransactionAsync(cancellationToken);
            throw;
        }
        if (publishAfterCommit)
        {
            foreach (var replaced in replacedSessions)
                await PublishSafeAsync(replaced, replaced.EndReason, cancellationToken);
            await PublishSafeAsync(session, "POS access session đã được tạo.", cancellationToken);
        }
        else
        {
            _pendingPublications.AddRange(replacedSessions.Select(x => (x, x.EndReason)));
            _pendingPublications.Add((session, "POS access session đã được tạo."));
        }
        return session;
    }

    public async Task FlushPendingPublicationsAsync(CancellationToken cancellationToken = default)
    {
        if (_pendingPublications.Count == 0) return;
        var pending = _pendingPublications.ToArray();
        _pendingPublications.Clear();
        foreach (var item in pending)
            await PublishSafeAsync(item.Session, item.Reason, cancellationToken);
    }

    public async Task<ServiceResult<PosAccessSessionDto>> ValidateAsync(
        Guid publicId, string jwtId, CancellationToken cancellationToken = default)
    {
        var session = await _repository.GetByPublicIdAsync(publicId, tracking: true, cancellationToken);
        if (session == null || !string.Equals(session.JwtId, jwtId, StringComparison.Ordinal))
            return ServiceResult<PosAccessSessionDto>.Failure("POS access session không hợp lệ.", errorCode: "POS_SESSION_INVALID");

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        if (session.Status != PosAccessSessionStatuses.Active)
            return ServiceResult<PosAccessSessionDto>.Failure(
                session.EndReason ?? "POS access session đã kết thúc.", errorCode: MapStatusError(session.Status));

        if (session.ExpiresAtUtc <= nowUtc)
        {
            return await EndAndRejectAsync(
                session,
                PosAccessSessionStatuses.Expired,
                null,
                "POS access session đã hết hạn.",
                "POS access session đã hết hạn.",
                "POS_SESSION_EXPIRED",
                cancellationToken);
        }

        if (!session.Account.Active || !session.Staff.Active || !session.Store.Active || !session.Terminal.Active)
        {
            return await EndAndRejectAsync(
                session,
                PosAccessSessionStatuses.TerminalLocked,
                null,
                "Tài khoản, cửa hàng hoặc Terminal không còn hoạt động.",
                "Terminal đã bị khóa hoặc thu hồi.",
                "POS_TERMINAL_LOCKED",
                cancellationToken);
        }

        if (session.Terminal.StoreId != session.StoreId
            || !string.Equals(session.Terminal.TerminalId, session.TerminalId, StringComparison.Ordinal))
        {
            return await EndAndRejectAsync(
                session,
                PosAccessSessionStatuses.Revoked,
                null,
                "Phiên POS không còn thuộc đúng nhân viên, cửa hàng hoặc Terminal.",
                "Bạn không thuộc cửa hàng hoặc Terminal này.",
                WorkShiftErrorCodes.PosAccessDenied,
                cancellationToken);
        }

        if (_permissions != null)
        {
            var permission = await _permissions.HasPermissionAsync(
                session.AccountId, PermissionConstants.AppPos, session.StoreId);
            if (!permission.IsSuccess || permission.Data?.Allowed != true)
            {
                return await EndAndRejectAsync(
                    session,
                    PosAccessSessionStatuses.Revoked,
                    null,
                    "Quyền sử dụng POS hoặc StaffScope đã bị thu hồi.",
                    "Bạn không có quyền truy cập POS tại cửa hàng này.",
                    WorkShiftErrorCodes.PosAccessDenied,
                    cancellationToken);
            }
        }

        if (session.WorkShiftId.HasValue)
        {
            var shift = session.WorkShift;
            if (shift == null
                || shift.ShiftId != session.WorkShiftId.Value
                || shift.UserId != session.StaffId
                || shift.StoreId != session.StoreId
                || !string.Equals(shift.PosTerminalId, session.TerminalId, StringComparison.Ordinal))
            {
                return await EndAndRejectAsync(
                    session,
                    PosAccessSessionStatuses.WorkShiftEnded,
                    null,
                    "Phiên POS không còn gắn với ca làm việc hợp lệ.",
                    "Ca làm việc không còn hợp lệ cho phiên POS này.",
                    WorkShiftErrorCodes.PosAccessDenied,
                    cancellationToken);
            }

            if (shift.Status is WorkShiftStatuses.Closed or WorkShiftStatuses.ReconciliationRequired)
            {
                return await EndAndRejectAsync(
                    session,
                    PosAccessSessionStatuses.WorkShiftEnded,
                    null,
                    "Ca làm việc đã kết thúc.",
                    "Ca làm việc đã kết thúc. Vui lòng quay lại StaffHub.",
                    WorkShiftErrorCodes.ShiftAlreadyClosed,
                    cancellationToken);
            }
        }

        return ServiceResult<PosAccessSessionDto>.Success(Map(session, nowUtc));
    }

    public async Task<ServiceResult<PosAccessSessionDto>> GetAsync(Guid publicId, CancellationToken cancellationToken = default)
    {
        var session = await _repository.GetByPublicIdAsync(publicId, tracking: false, cancellationToken);
        return session == null
            ? ServiceResult<PosAccessSessionDto>.Failure("Không tìm thấy POS access session.", errorCode: "POS_SESSION_INVALID")
            : ServiceResult<PosAccessSessionDto>.Success(Map(session, _timeProvider.GetUtcNow().UtcDateTime));
    }

    public async Task<ServiceResult<IReadOnlyList<PosAccessSessionDto>>> GetActiveAsync(
        IReadOnlyCollection<int> storeIds, CancellationToken cancellationToken = default)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var sessions = await _repository.GetActiveForStoresAsync(storeIds, cancellationToken);
        return ServiceResult<IReadOnlyList<PosAccessSessionDto>>.Success(
            sessions.Select(x => Map(x, nowUtc)).ToArray());
    }

    public async Task<ServiceResult> BindWorkShiftAsync(Guid publicId, int workShiftId, CancellationToken cancellationToken = default)
    {
        if (publicId == Guid.Empty || workShiftId <= 0) return ServiceResult.Failure("Session/WorkShift không hợp lệ.");
        try
        {
            await _repository.BindWorkShiftAsync(publicId, workShiftId, cancellationToken);
            return ServiceResult.Success("Đã bind WorkShift vào POS access session.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            return ServiceResult.Failure(ex.Message, errorCode: "POS_SESSION_WORKSHIFT_MISMATCH");
        }
    }

    public async Task<ServiceResult> EndAsync(
        Guid publicId, string status, int? endedByStaffId, string reason,
        CancellationToken cancellationToken = default)
    {
        var session = await _repository.GetByPublicIdAsync(publicId, tracking: true, cancellationToken);
        if (session == null) return ServiceResult.Failure("Không tìm thấy POS access session.", errorCode: "POS_SESSION_INVALID");
        if (session.Status != PosAccessSessionStatuses.Active) return ServiceResult.Success("POS access session đã kết thúc trước đó.");
        var transition = await EndSessionAsync(session, status, endedByStaffId, reason, cancellationToken);
        if (transition.Session == null)
            return ServiceResult.Failure("POS access session không còn tồn tại.", errorCode: "POS_SESSION_INVALID");
        if (transition.HasConflict)
            return ServiceResult.Failure(
                "Trạng thái POS access session đang được cập nhật. Vui lòng thử lại.",
                errorCode: SessionConflictErrorCode);
        return ServiceResult.Success(transition.EndedNow
            ? "Đã kết thúc POS access session."
            : "POS access session đã kết thúc trước đó.");
    }

    public async Task<int> ExpireDueAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var due = await _repository.GetDueForExpiryAsync(nowUtc, 200, cancellationToken);
        var expiredCount = 0;
        foreach (var session in due)
        {
            var transition = await EndSessionAsync(
                session,
                PosAccessSessionStatuses.Expired,
                null,
                "POS access session đã hết hạn.",
                cancellationToken,
                auditAction: "POS_SESSION_EXPIRED");
            if (transition.EndedNow) expiredCount++;
        }
        return expiredCount;
    }

    private async Task<ServiceResult<PosAccessSessionDto>> EndAndRejectAsync(
        PosAccessSession session,
        string status,
        int? actor,
        string endReason,
        string rejectionMessage,
        string rejectionErrorCode,
        CancellationToken cancellationToken)
    {
        var transition = await EndSessionAsync(session, status, actor, endReason, cancellationToken);
        if (transition.Session == null)
            return ServiceResult<PosAccessSessionDto>.Failure(
                "POS access session không hợp lệ.", errorCode: "POS_SESSION_INVALID");
        if (transition.HasConflict)
            return ServiceResult<PosAccessSessionDto>.Failure(
                "Trạng thái POS access session đang được cập nhật. Vui lòng thử lại.",
                errorCode: SessionConflictErrorCode);
        if (transition.EndedNow || transition.Session.Status == status)
            return ServiceResult<PosAccessSessionDto>.Failure(rejectionMessage, errorCode: rejectionErrorCode);
        return ServiceResult<PosAccessSessionDto>.Failure(
            transition.Session.EndReason ?? "POS access session đã kết thúc.",
            errorCode: MapStatusError(transition.Session.Status));
    }

    private async Task<EndSessionTransition> EndSessionAsync(
        PosAccessSession session,
        string status,
        int? actor,
        string reason,
        CancellationToken cancellationToken,
        string auditAction = "POS_SESSION_ENDED")
    {
        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? "POS access session đã kết thúc."
            : reason.Trim();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var endedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            await _repository.BeginTransactionAsync(cancellationToken);
            try
            {
                var endedNow = await _repository.TryEndActiveAsync(
                    session.PublicId,
                    status,
                    endedAtUtc,
                    actor,
                    normalizedReason,
                    cancellationToken);
                if (endedNow)
                {
                    if (_audit != null)
                        await _audit.WriteAsync(
                            auditAction,
                            session.WorkShiftId ?? 0,
                            actor ?? session.StaffId,
                            new { OldStatus = PosAccessSessionStatuses.Active },
                            new { Status = status, EndReason = normalizedReason, EndedAtUtc = endedAtUtc },
                            cancellationToken);
                    await _repository.CommitTransactionAsync(cancellationToken);
                    var endedSession = await _repository.GetByPublicIdAsync(
                        session.PublicId, tracking: false, cancellationToken);
                    if (endedSession != null)
                        await PublishSafeAsync(endedSession, endedSession.EndReason, cancellationToken);
                    return new EndSessionTransition(endedSession, EndedNow: true, HasConflict: false);
                }
                await _repository.RollbackTransactionAsync(cancellationToken);
            }
            catch
            {
                await _repository.RollbackTransactionAsync(cancellationToken);
                throw;
            }

            var current = await _repository.GetByPublicIdAsync(
                session.PublicId, tracking: false, cancellationToken);
            if (current == null)
                return new EndSessionTransition(null, EndedNow: false, HasConflict: false);
            if (current.Status != PosAccessSessionStatuses.Active)
                return new EndSessionTransition(current, EndedNow: false, HasConflict: false);
        }

        _logger.LogWarning(
            "POS access session {SessionId} remained ACTIVE after two atomic end attempts.",
            session.PublicId);
        return new EndSessionTransition(session, EndedNow: false, HasConflict: true);
    }

    private async Task PublishSafeAsync(PosAccessSession session, string? reason, CancellationToken cancellationToken)
    {
        if (_publisher == null) return;
        try
        {
            await _publisher.PublishAsync(new PosAccessSessionChangedDto
            {
                SessionId = session.PublicId,
                StoreId = session.StoreId,
                TerminalId = session.TerminalId,
                Status = session.Status,
                Reason = reason,
                OccurredAtUtc = _timeProvider.GetUtcNow().UtcDateTime
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Unable to publish POS access session {SessionId} status {Status}; clients will recover by validation/polling.",
                session.PublicId, session.Status);
        }
    }

    private static PosAccessSessionDto Map(PosAccessSession session, DateTime nowUtc)
    {
        var workShiftStatus = session.WorkShift?.Status;
        var accessMode = !session.WorkShiftId.HasValue
            ? PosAccessModes.OpeningCash
            : workShiftStatus == WorkShiftStatuses.Open
                ? PosAccessModes.Active
                : PosAccessModes.PendingClose;
        var recommendedAction = accessMode switch
        {
            PosAccessModes.Active => WorkShiftRecommendedActions.ContinuePos,
            PosAccessModes.PendingClose => WorkShiftRecommendedActions.CompleteClosing,
            _ => WorkShiftRecommendedActions.EnterOpeningCash
        };
        return new PosAccessSessionDto
        {
        SessionId = session.PublicId,
        AccountId = session.AccountId,
        StaffId = session.StaffId,
        StoreId = session.StoreId,
        TerminalId = session.TerminalId,
        WorkShiftId = session.WorkShiftId,
        Status = session.Status,
        AccessMode = accessMode,
        WorkShiftStatus = workShiftStatus,
        RecommendedAction = recommendedAction,
        IssuedAtUtc = session.IssuedAtUtc,
        ExpiresAtUtc = session.ExpiresAtUtc,
        ServerNowUtc = nowUtc,
        EndReason = session.EndReason
        };
    }

    private static string MapStatusError(string status) => status switch
    {
        PosAccessSessionStatuses.Expired => "POS_SESSION_EXPIRED",
        PosAccessSessionStatuses.TerminalLocked => "POS_TERMINAL_LOCKED",
        PosAccessSessionStatuses.LoggedOut => "POS_SESSION_ENDED",
        PosAccessSessionStatuses.AdminEnded => "POS_SESSION_ENDED",
        PosAccessSessionStatuses.WorkShiftEnded => WorkShiftErrorCodes.ShiftAlreadyClosed,
        _ => "POS_SESSION_REVOKED"
    };

    private sealed record EndSessionTransition(
        PosAccessSession? Session,
        bool EndedNow,
        bool HasConflict);
}
