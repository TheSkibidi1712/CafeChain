using System.IdentityModel.Tokens.Jwt;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Services.POS;
using CafeChain.Models.Customers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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

    private static PosSessionExchangeService CreateService(CafeChain.Data.AppDbContext db)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "CafeChain.Tests.PosSession.Exchange.Signing.Key.2026",
            ["Jwt:Issuer"] = "CafeChain.Tests",
            ["Jwt:Audience"] = "CafeChain.POS"
        }).Build();
        return new PosSessionExchangeService(db, configuration, TimeProvider.System);
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
