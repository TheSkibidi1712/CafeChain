using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Operations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeChain.Application.Services.POS;

public sealed class PosAccessSessionService : IPosAccessSessionService
{
    private readonly IPosAccessSessionRepository _repository;
    private readonly IPosAccessSessionPublisher? _publisher;
    private readonly IWorkShiftAuditService? _audit;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PosAccessSessionService> _logger;
    private readonly List<(PosAccessSession Session, string? Reason)> _pendingPublications = [];

    public PosAccessSessionService(
        IPosAccessSessionRepository repository,
        IPosAccessSessionPublisher? publisher = null,
        IWorkShiftAuditService? audit = null,
        TimeProvider? timeProvider = null,
        ILogger<PosAccessSessionService>? logger = null)
    {
        _repository = repository;
        _publisher = publisher;
        _audit = audit;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<PosAccessSessionService>.Instance;
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
            await EndTrackedAsync(session, PosAccessSessionStatuses.Expired, null,
                "POS access session đã hết hạn.", cancellationToken);
            return ServiceResult<PosAccessSessionDto>.Failure("POS access session đã hết hạn.", errorCode: "POS_SESSION_EXPIRED");
        }

        if (!session.Account.Active || !session.Staff.Active || !session.Store.Active || !session.Terminal.Active)
        {
            await EndTrackedAsync(session, PosAccessSessionStatuses.TerminalLocked, null,
                "Tài khoản, cửa hàng hoặc Terminal không còn hoạt động.", cancellationToken);
            return ServiceResult<PosAccessSessionDto>.Failure(
                "Terminal đã bị khóa hoặc thu hồi.", errorCode: "POS_TERMINAL_LOCKED");
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
        await EndTrackedAsync(session, status, endedByStaffId, reason, cancellationToken);
        return ServiceResult.Success("Đã kết thúc POS access session.");
    }

    public async Task<int> ExpireDueAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await _repository.BeginTransactionAsync(cancellationToken);
        IReadOnlyList<PosAccessSession> due;
        try
        {
            due = await _repository.GetDueForExpiryAsync(nowUtc, 200, cancellationToken);
            if (due.Count == 0)
            {
                await _repository.CommitTransactionAsync(cancellationToken);
                return 0;
            }

            foreach (var session in due)
            {
                session.Status = PosAccessSessionStatuses.Expired;
                session.EndedAtUtc = nowUtc;
                session.EndReason = "POS access session đã hết hạn.";
            }
            await _repository.SaveChangesAsync(cancellationToken);
            if (_audit != null)
            {
                foreach (var session in due)
                    await _audit.WriteAsync(
                        "POS_SESSION_EXPIRED",
                        session.WorkShiftId ?? 0,
                        session.StaffId,
                        new { Status = PosAccessSessionStatuses.Active },
                        new { session.PublicId, session.Status, session.ExpiresAtUtc, session.EndedAtUtc },
                        cancellationToken);
            }
            await _repository.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _repository.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        foreach (var session in due)
            await PublishSafeAsync(session, session.EndReason, cancellationToken);
        return due.Count;
    }

    private async Task EndTrackedAsync(PosAccessSession session, string status, int? actor, string reason,
        CancellationToken cancellationToken)
    {
        await _repository.BeginTransactionAsync(cancellationToken);
        try
        {
            session.Status = status;
            session.EndedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            session.EndedByStaffId = actor;
            session.EndReason = string.IsNullOrWhiteSpace(reason) ? "POS access session đã kết thúc." : reason.Trim();
            await _repository.SaveChangesAsync(cancellationToken);
            if (_audit != null)
                await _audit.WriteAsync("POS_SESSION_ENDED", session.WorkShiftId ?? 0, actor ?? session.StaffId,
                    new { OldStatus = PosAccessSessionStatuses.Active },
                    new { session.Status, session.EndReason, session.EndedAtUtc }, cancellationToken);
            await _repository.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _repository.RollbackTransactionAsync(cancellationToken);
            throw;
        }
        await PublishSafeAsync(session, session.EndReason, cancellationToken);
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

    private static PosAccessSessionDto Map(PosAccessSession session, DateTime nowUtc) => new()
    {
        SessionId = session.PublicId,
        AccountId = session.AccountId,
        StaffId = session.StaffId,
        StoreId = session.StoreId,
        TerminalId = session.TerminalId,
        WorkShiftId = session.WorkShiftId,
        Status = session.Status,
        IssuedAtUtc = session.IssuedAtUtc,
        ExpiresAtUtc = session.ExpiresAtUtc,
        ServerNowUtc = nowUtc,
        EndReason = session.EndReason
    };

    private static string MapStatusError(string status) => status switch
    {
        PosAccessSessionStatuses.Expired => "POS_SESSION_EXPIRED",
        PosAccessSessionStatuses.TerminalLocked => "POS_TERMINAL_LOCKED",
        PosAccessSessionStatuses.LoggedOut => "POS_SESSION_ENDED",
        PosAccessSessionStatuses.AdminEnded => "POS_SESSION_ENDED",
        _ => "POS_SESSION_REVOKED"
    };
}
