using CafeChain.Application.DTOs.POS;
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
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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

    private static Mock<IPosAccessSessionRepository> RepositoryReturning(PosAccessSession session)
    {
        var repository = new Mock<IPosAccessSessionRepository>();
        repository.Setup(x => x.GetByPublicIdAsync(session.PublicId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
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
        ExpiresAtUtc = expiresAtUtc
    };

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
