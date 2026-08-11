using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Services.POS;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Customers;
using CafeChain.Models.Operations;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Moq;

namespace CafeChain.Tests.POS;

public sealed class PosAccessSessionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_replaces_previous_terminal_session_and_publishes_both_after_repository_commit()
    {
        var previous = Session(PosAccessSessionStatuses.Replaced, Now.AddHours(1).UtcDateTime);
        previous.PublicId = Guid.NewGuid();
        var repository = new Mock<IPosAccessSessionRepository>();
        repository.Setup(x => x.CreateReplacingActiveAsync(
                It.IsAny<PosAccessSession>(), Now.UtcDateTime, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { previous });
        var publisher = new Mock<IPosAccessSessionPublisher>();
        var published = new List<PosAccessSessionChangedDto>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<PosAccessSessionChangedDto>(), It.IsAny<CancellationToken>()))
            .Callback<PosAccessSessionChangedDto, CancellationToken>((value, _) => published.Add(value))
            .Returns(Task.CompletedTask);

        var created = await new PosAccessSessionService(
                repository.Object, publisher.Object, timeProvider: new FixedTimeProvider(Now))
            .CreateAsync(10, 20, 30, " POS-01 ", 40, null, Now.AddHours(6).UtcDateTime);

        Assert.Equal("POS-01", created.TerminalId);
        Assert.Equal(PosAccessSessionStatuses.Active, created.Status);
        Assert.Collection(published,
            value =>
            {
                Assert.Equal(PosAccessSessionStatuses.Replaced, value.Status);
                Assert.Equal(previous.StoreId, value.StoreId);
            },
            value =>
            {
                Assert.Equal(PosAccessSessionStatuses.Active, value.Status);
                Assert.Equal(30, value.StoreId);
            });
    }

    [Fact]
    public async Task Expired_session_is_persisted_and_rejected_with_stable_error_code()
    {
        var expired = Session(PosAccessSessionStatuses.Active, Now.AddSeconds(-1).UtcDateTime);
        var repository = RepositoryReturning(expired);
        var publisher = new Mock<IPosAccessSessionPublisher>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<PosAccessSessionChangedDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await new PosAccessSessionService(
                repository.Object, publisher.Object, timeProvider: new FixedTimeProvider(Now))
            .ValidateAsync(expired.PublicId, expired.JwtId);

        Assert.False(result.IsSuccess);
        Assert.Equal("POS_SESSION_EXPIRED", result.ErrorCode);
        Assert.Equal(PosAccessSessionStatuses.Expired, expired.Status);
        repository.Verify(x => x.TryEndActiveAsync(
            expired.PublicId,
            PosAccessSessionStatuses.Expired,
            Now.UtcDateTime,
            null,
            "POS access session đã hết hạn.",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Inactive_terminal_revokes_session_instead_of_reporting_workshift_expiry()
    {
        var session = Session(PosAccessSessionStatuses.Active, Now.AddHours(1).UtcDateTime);
        session.Account = new Account { Active = true };
        session.Staff = new Staff { Active = true };
        session.Store = new Store { Active = true };
        session.Terminal = new PosTerminal { Active = false };
        var repository = RepositoryReturning(session);

        var result = await new PosAccessSessionService(
                repository.Object, timeProvider: new FixedTimeProvider(Now))
            .ValidateAsync(session.PublicId, session.JwtId);

        Assert.False(result.IsSuccess);
        Assert.Equal("POS_TERMINAL_LOCKED", result.ErrorCode);
        Assert.Equal(PosAccessSessionStatuses.TerminalLocked, session.Status);
    }

    [Fact]
    public async Task Bound_closed_workshift_ends_session_and_denies_direct_pos_access()
    {
        var session = Session(PosAccessSessionStatuses.Active, Now.AddHours(1).UtcDateTime);
        session.WorkShiftId = 501;
        session.WorkShift = WorkShift(501, WorkShiftStatuses.Closed);
        var repository = RepositoryReturning(session);

        var result = await new PosAccessSessionService(
                repository.Object, timeProvider: new FixedTimeProvider(Now))
            .ValidateAsync(session.PublicId, session.JwtId);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkShiftErrorCodes.ShiftAlreadyClosed, result.ErrorCode);
        Assert.Equal(PosAccessSessionStatuses.WorkShiftEnded, session.Status);
    }

    [Theory]
    [InlineData(WorkShiftStatuses.Open, PosAccessModes.Active)]
    [InlineData(WorkShiftStatuses.Closing, PosAccessModes.PendingClose)]
    [InlineData(WorkShiftStatuses.ExpiredPendingClose, PosAccessModes.PendingClose)]
    public async Task Bound_workshift_returns_authoritative_access_mode(string status, string expectedMode)
    {
        var session = Session(PosAccessSessionStatuses.Active, Now.AddHours(1).UtcDateTime);
        session.WorkShiftId = 501;
        session.WorkShift = WorkShift(501, status);

        var result = await new PosAccessSessionService(
                RepositoryReturning(session).Object, timeProvider: new FixedTimeProvider(Now))
            .ValidateAsync(session.PublicId, session.JwtId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(expectedMode, result.Data!.AccessMode);
        Assert.Equal(status, result.Data.WorkShiftStatus);
    }

    [Fact]
    public async Task Unbound_session_only_allows_opening_cash_mode()
    {
        var session = Session(PosAccessSessionStatuses.Active, Now.AddHours(1).UtcDateTime);
        var result = await new PosAccessSessionService(
                RepositoryReturning(session).Object, timeProvider: new FixedTimeProvider(Now))
            .ValidateAsync(session.PublicId, session.JwtId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(PosAccessModes.OpeningCash, result.Data!.AccessMode);
        Assert.Null(result.Data.WorkShiftId);
    }

    [Fact]
    public async Task SignalR_failure_does_not_undo_a_committed_session()
    {
        var repository = new Mock<IPosAccessSessionRepository>();
        repository.Setup(x => x.CreateReplacingActiveAsync(
                It.IsAny<PosAccessSession>(), Now.UtcDateTime, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PosAccessSession>());
        var publisher = new Mock<IPosAccessSessionPublisher>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<PosAccessSessionChangedDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR disconnected"));

        var created = await new PosAccessSessionService(
                repository.Object, publisher.Object, timeProvider: new FixedTimeProvider(Now))
            .CreateAsync(10, 20, 30, "POS-01", 40, null, Now.AddHours(6).UtcDateTime);

        Assert.Equal(PosAccessSessionStatuses.Active, created.Status);
        repository.Verify(x => x.CreateReplacingActiveAsync(
            created, Now.UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Outer_transaction_can_defer_session_events_until_commit()
    {
        var repository = new Mock<IPosAccessSessionRepository>();
        repository.Setup(x => x.CreateReplacingActiveAsync(
                It.IsAny<PosAccessSession>(), Now.UtcDateTime, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PosAccessSession>());
        var publisher = new Mock<IPosAccessSessionPublisher>();
        publisher.Setup(x => x.PublishAsync(It.IsAny<PosAccessSessionChangedDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var service = new PosAccessSessionService(
            repository.Object, publisher.Object, timeProvider: new FixedTimeProvider(Now));

        await service.CreateAsync(
            10, 20, 30, "POS-01", 40, null, Now.AddHours(6).UtcDateTime,
            publishAfterCommit: false);

        publisher.Verify(x => x.PublishAsync(
            It.IsAny<PosAccessSessionChangedDto>(), It.IsAny<CancellationToken>()), Times.Never);

        await service.FlushPendingPublicationsAsync();

        publisher.Verify(x => x.PublishAsync(
            It.Is<PosAccessSessionChangedDto>(value => value.Status == PosAccessSessionStatuses.Active),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Due_sessions_are_expired_and_published_only_after_commit()
    {
        var first = Session(PosAccessSessionStatuses.Active, Now.AddSeconds(-2).UtcDateTime);
        var second = Session(PosAccessSessionStatuses.Active, Now.AddSeconds(-1).UtcDateTime);
        var repository = new Mock<IPosAccessSessionRepository>();
        repository.Setup(x => x.GetDueForExpiryAsync(
                Now.UtcDateTime, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second });
        repository.Setup(x => x.TryEndActiveAsync(
                It.IsAny<Guid>(),
                PosAccessSessionStatuses.Expired,
                Now.UtcDateTime,
                null,
                "POS access session đã hết hạn.",
                It.IsAny<CancellationToken>()))
            .Callback<Guid, string, DateTime, int?, string, CancellationToken>((id, status, endedAtUtc, actor, reason, _) =>
            {
                var session = new[] { first, second }.Single(x => x.PublicId == id);
                session.Status = status;
                session.EndedAtUtc = endedAtUtc;
                session.EndedByStaffId = actor;
                session.EndReason = reason;
            })
            .ReturnsAsync(true);
        repository.Setup(x => x.GetByPublicIdAsync(
                It.IsAny<Guid>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, bool _, CancellationToken _) =>
                new[] { first, second }.SingleOrDefault(x => x.PublicId == id));
        repository.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var committed = false;
        repository.Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Callback(() => committed = true)
            .Returns(Task.CompletedTask);
        var publisher = new Mock<IPosAccessSessionPublisher>();
        publisher.Setup(x => x.PublishAsync(
                It.IsAny<PosAccessSessionChangedDto>(), It.IsAny<CancellationToken>()))
            .Callback(() => Assert.True(committed))
            .Returns(Task.CompletedTask);
        var audit = new Mock<IWorkShiftAuditService>();
        var service = new PosAccessSessionService(
            repository.Object, publisher.Object, audit.Object, new FixedTimeProvider(Now));

        var count = await service.ExpireDueAsync();

        Assert.Equal(2, count);
        Assert.All(new[] { first, second }, session =>
        {
            Assert.Equal(PosAccessSessionStatuses.Expired, session.Status);
            Assert.Equal(Now.UtcDateTime, session.EndedAtUtc);
            Assert.Equal("POS access session đã hết hạn.", session.EndReason);
        });
        audit.Verify(x => x.WriteAsync(
            "POS_SESSION_EXPIRED",
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<object>(),
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        publisher.Verify(x => x.PublishAsync(
            It.Is<PosAccessSessionChangedDto>(value => value.Status == PosAccessSessionStatuses.Expired),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Concurrent_closed_workshift_validation_loser_reads_final_state_without_duplicate_side_effects()
    {
        var stale = Session(PosAccessSessionStatuses.Active, Now.AddHours(1).UtcDateTime);
        stale.WorkShiftId = 501;
        stale.WorkShift = WorkShift(501, WorkShiftStatuses.Closed);
        var ended = Session(PosAccessSessionStatuses.WorkShiftEnded, Now.AddHours(1).UtcDateTime);
        ended.PublicId = stale.PublicId;
        ended.JwtId = stale.JwtId;
        ended.WorkShiftId = stale.WorkShiftId;
        ended.WorkShift = stale.WorkShift;
        ended.EndReason = "Ca làm việc đã kết thúc.";

        var repository = new Mock<IPosAccessSessionRepository>();
        repository.Setup(x => x.GetByPublicIdAsync(stale.PublicId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stale);
        repository.Setup(x => x.TryEndActiveAsync(
                stale.PublicId,
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repository.Setup(x => x.GetByPublicIdAsync(stale.PublicId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ended);
        var audit = new Mock<IWorkShiftAuditService>();
        var publisher = new Mock<IPosAccessSessionPublisher>();

        var result = await new PosAccessSessionService(
                repository.Object, publisher.Object, audit.Object, new FixedTimeProvider(Now))
            .ValidateAsync(stale.PublicId, stale.JwtId);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkShiftErrorCodes.ShiftAlreadyClosed, result.ErrorCode);
        audit.Verify(x => x.WriteAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<object>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        publisher.Verify(x => x.PublishAsync(
            It.IsAny<PosAccessSessionChangedDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Concurrent_different_terminal_state_wins_and_preserves_its_error_code()
    {
        var stale = Session(PosAccessSessionStatuses.Active, Now.AddHours(1).UtcDateTime);
        stale.WorkShiftId = 501;
        stale.WorkShift = WorkShift(501, WorkShiftStatuses.Closed);
        var revoked = Session(PosAccessSessionStatuses.Revoked, Now.AddHours(1).UtcDateTime);
        revoked.PublicId = stale.PublicId;
        revoked.JwtId = stale.JwtId;
        revoked.EndReason = "Phiên POS đã bị thu hồi.";

        var repository = new Mock<IPosAccessSessionRepository>();
        repository.Setup(x => x.GetByPublicIdAsync(stale.PublicId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stale);
        repository.Setup(x => x.TryEndActiveAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int?>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repository.Setup(x => x.GetByPublicIdAsync(stale.PublicId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(revoked);

        var result = await new PosAccessSessionService(
                repository.Object, timeProvider: new FixedTimeProvider(Now))
            .ValidateAsync(stale.PublicId, stale.JwtId);

        Assert.False(result.IsSuccess);
        Assert.Equal("POS_SESSION_REVOKED", result.ErrorCode);
        Assert.Equal(revoked.EndReason, result.Message);
    }

    [Fact]
    public async Task Session_deleted_after_read_returns_stable_invalid_error()
    {
        var stale = Session(PosAccessSessionStatuses.Active, Now.AddSeconds(-1).UtcDateTime);
        var repository = new Mock<IPosAccessSessionRepository>();
        repository.Setup(x => x.GetByPublicIdAsync(stale.PublicId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stale);
        repository.Setup(x => x.TryEndActiveAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int?>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repository.Setup(x => x.GetByPublicIdAsync(stale.PublicId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PosAccessSession?)null);

        var result = await new PosAccessSessionService(
                repository.Object, timeProvider: new FixedTimeProvider(Now))
            .ValidateAsync(stale.PublicId, stale.JwtId);

        Assert.False(result.IsSuccess);
        Assert.Equal("POS_SESSION_INVALID", result.ErrorCode);
    }

    [Fact]
    public async Task Active_session_after_one_retry_returns_stable_conflict_instead_of_throwing()
    {
        var stale = Session(PosAccessSessionStatuses.Active, Now.AddSeconds(-1).UtcDateTime);
        var repository = new Mock<IPosAccessSessionRepository>();
        repository.Setup(x => x.GetByPublicIdAsync(stale.PublicId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stale);
        repository.Setup(x => x.TryEndActiveAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int?>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repository.Setup(x => x.GetByPublicIdAsync(stale.PublicId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stale);

        var result = await new PosAccessSessionService(
                repository.Object, timeProvider: new FixedTimeProvider(Now))
            .ValidateAsync(stale.PublicId, stale.JwtId);

        Assert.False(result.IsSuccess);
        Assert.Equal("POS_SESSION_CONFLICT", result.ErrorCode);
        repository.Verify(x => x.TryEndActiveAsync(
            stale.PublicId,
            PosAccessSessionStatuses.Expired,
            It.IsAny<DateTime>(),
            null,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static Mock<IPosAccessSessionRepository> RepositoryReturning(PosAccessSession session)
    {
        var repository = new Mock<IPosAccessSessionRepository>();
        repository.Setup(x => x.GetByPublicIdAsync(session.PublicId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        repository.Setup(x => x.GetByPublicIdAsync(session.PublicId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        repository.Setup(x => x.TryEndActiveAsync(
                session.PublicId,
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<int?>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, string, DateTime, int?, string, CancellationToken>((_, status, endedAtUtc, actor, reason, _) =>
            {
                session.Status = status;
                session.EndedAtUtc = endedAtUtc;
                session.EndedByStaffId = actor;
                session.EndReason = reason;
            })
            .ReturnsAsync(true);
        return repository;
    }

    private static PosAccessSession Session(string status, DateTime expiresAtUtc) => new()
    {
        PublicId = Guid.NewGuid(),
        JwtId = Guid.NewGuid().ToString("N"),
        AccountId = 10,
        StaffId = 20,
        StoreId = 30,
        TerminalId = "POS-01",
        ExchangeContextId = 40,
        Status = status,
        IssuedAtUtc = Now.UtcDateTime,
        ExpiresAtUtc = expiresAtUtc,
        Account = new Account { AccountId = 10, Active = true },
        Staff = new Staff { StaffId = 20, Active = true },
        Store = new Store { StoreId = 30, Active = true },
        Terminal = new PosTerminal { TerminalId = "POS-01", StoreId = 30, Active = true }
    };

    private static WorkShift WorkShift(int id, string status) => new()
    {
        ShiftId = id,
        UserId = 20,
        StoreId = 30,
        PosTerminalId = "POS-01",
        Status = status
    };

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
