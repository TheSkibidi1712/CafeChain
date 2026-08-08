using System.IdentityModel.Tokens.Jwt;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Services.POS;
using CafeChain.Models.Customers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;

namespace CafeChain.Tests.POS;

public sealed class PosSessionExchangeServiceTests : IntegrationTestBase
{
    [Fact]
    public async Task Null_terminal_context_fails_ticket_without_throwing()
    {
        await using var db = CreateDbContext();
        await SeedActorAsync(db);
        var service = CreateService(db);
        var ticket = await service.IssueAsync(new PosSessionExchangeContextDto
        {
            Purpose = PosSessionPurposes.ResumeWorkShift,
            AccountId = 10,
            StaffId = 20,
            StoreId = 30,
            TerminalId = null,
            WorkShiftId = 40
        });

        var result = await service.ExchangeAsync(ticket.ExchangeCode);

        Assert.False(result.IsSuccess);
        Assert.Equal(PosSessionExchangeErrorCodes.ContextInvalid, result.ErrorCode);
        var stored = await db.RequestDeduplications.SingleAsync(
            x => x.RequestDeduplicationId == ticket.ContextId);
        Assert.Equal("FAILED", stored.Status);
        Assert.Null(stored.ProcessingLeaseUntilUtc);
    }

    [Fact]
    public async Task Valid_context_issues_jwt_with_terminal_claim()
    {
        await using var db = CreateDbContext();
        await SeedActorAsync(db);
        var service = CreateService(db);
        var ticket = await service.IssueAsync(new PosSessionExchangeContextDto
        {
            Purpose = PosSessionPurposes.ResumeWorkShift,
            AccountId = 10,
            StaffId = 20,
            StoreId = 30,
            TerminalId = "POS-1",
            WorkShiftId = 40
        });

        var result = await service.ExchangeAsync(ticket.ExchangeCode);

        Assert.True(result.IsSuccess, result.Message);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Data!.Token);
        Assert.Equal("POS-1", jwt.Claims.Single(x => x.Type == "PosTerminalId").Value);
        Assert.Equal("SUCCESS", (await db.RequestDeduplications.SingleAsync(
            x => x.RequestDeduplicationId == ticket.ContextId)).Status);
    }

    [Fact]
    public async Task Cancel_opening_marks_exchange_expired_and_is_idempotent()
    {
        await using var db = CreateDbContext();
        await SeedActorAsync(db);
        var sessionId = Guid.NewGuid();
        var sessions = new Mock<CafeChain.Application.Interfaces.POS.IPosAccessSessionService>();
        sessions.Setup(x => x.CreateAsync(
                10, 20, 30, "POS-1", It.IsAny<int>(), null, It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(new CafeChain.Models.Operations.PosAccessSession
            {
                PublicId = sessionId,
                JwtId = Guid.NewGuid().ToString("N"),
                AccountId = 10,
                StaffId = 20,
                StoreId = 30,
                TerminalId = "POS-1",
                Status = CafeChain.Models.Operations.PosAccessSessionStatuses.Active,
                IssuedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
            });
        sessions.Setup(x => x.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CafeChain.Application.Results.ServiceResult<PosAccessSessionDto>.Success(
                new PosAccessSessionDto { SessionId = sessionId, WorkShiftId = null }));
        sessions.Setup(x => x.EndAsync(
                sessionId,
                CafeChain.Models.Operations.PosAccessSessionStatuses.LoggedOut,
                20,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CafeChain.Application.Results.ServiceResult.Success());
        var service = CreateService(db, sessions.Object);
        var ticket = await service.IssueAsync(new PosSessionExchangeContextDto
        {
            Purpose = PosSessionPurposes.OpenWorkShift,
            AccountId = 10,
            StaffId = 20,
            StoreId = 30,
            TerminalId = "POS-1",
            RequestKey = "cancel-opening-test",
            RequiresStaffHubOpen = true,
            RequiresOpeningCash = true
        });
        Assert.True((await service.ExchangeAsync(ticket.ExchangeCode)).IsSuccess);

        var first = await service.CancelOpeningAsync(ticket.ContextId, 10, 20, 30, sessionId);
        var second = await service.CancelOpeningAsync(ticket.ContextId, 10, 20, 30, sessionId);

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(second.IsSuccess, second.Message);
        Assert.NotNull(first.Data!.CancelledAtUtc);
        Assert.False(first.Data.RequiresOpeningCash);
        var stored = await db.RequestDeduplications.SingleAsync(
            x => x.RequestDeduplicationId == ticket.ContextId);
        Assert.Equal("EXPIRED", stored.Status);
        Assert.Contains("CancelledAtUtc", stored.ResponseBody, StringComparison.Ordinal);
        sessions.Verify(x => x.EndAsync(
            sessionId,
            CafeChain.Models.Operations.PosAccessSessionStatuses.LoggedOut,
            20,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Cancel_opening_rejects_session_already_bound_to_workshift()
    {
        await using var db = CreateDbContext();
        await SeedActorAsync(db);
        var sessionId = Guid.NewGuid();
        var sessions = new Mock<CafeChain.Application.Interfaces.POS.IPosAccessSessionService>();
        sessions.Setup(x => x.CreateAsync(
                10, 20, 30, "POS-1", It.IsAny<int>(), null, It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(new CafeChain.Models.Operations.PosAccessSession
            {
                PublicId = sessionId,
                JwtId = Guid.NewGuid().ToString("N"),
                AccountId = 10,
                StaffId = 20,
                StoreId = 30,
                TerminalId = "POS-1",
                Status = CafeChain.Models.Operations.PosAccessSessionStatuses.Active,
                IssuedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
            });
        sessions.Setup(x => x.GetAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CafeChain.Application.Results.ServiceResult<PosAccessSessionDto>.Success(
                new PosAccessSessionDto { SessionId = sessionId, WorkShiftId = 99 }));
        var service = CreateService(db, sessions.Object);
        var ticket = await service.IssueAsync(new PosSessionExchangeContextDto
        {
            Purpose = PosSessionPurposes.OpenWorkShift,
            AccountId = 10,
            StaffId = 20,
            StoreId = 30,
            TerminalId = "POS-1",
            RequestKey = "bound-opening-test",
            RequiresStaffHubOpen = true,
            RequiresOpeningCash = true
        });
        Assert.True((await service.ExchangeAsync(ticket.ExchangeCode)).IsSuccess);

        var result = await service.CancelOpeningAsync(ticket.ContextId, 10, 20, 30, sessionId);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkShiftErrorCodes.ConcurrencyConflict, result.ErrorCode);
        Assert.Equal("SUCCESS", (await db.RequestDeduplications.SingleAsync(
            x => x.RequestDeduplicationId == ticket.ContextId)).Status);
        sessions.Verify(x => x.EndAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static PosSessionExchangeService CreateService(
        CafeChain.Data.AppDbContext db,
        CafeChain.Application.Interfaces.POS.IPosAccessSessionService? sessions = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "CafeChain.Tests.PosSession.Exchange.Signing.Key.2026",
            ["Jwt:Issuer"] = "CafeChain.Tests",
            ["Jwt:Audience"] = "CafeChain.POS"
        }).Build();
        return new PosSessionExchangeService(db, configuration, TimeProvider.System, sessions);
    }

    private static async Task SeedActorAsync(CafeChain.Data.AppDbContext db)
    {
        db.Stores.Add(new Store
        {
            StoreId = 30,
            Name = "POS Test Store",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
        db.Accounts.Add(new Account
        {
            AccountId = 10,
            Email = "pos-exchange@test.local",
            PasswordHash = "x",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
        db.Staffs.Add(new Staff
        {
            StaffId = 20,
            AccountId = 10,
            StoreId = 30,
            FullName = "POS Exchange Test",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }
}
