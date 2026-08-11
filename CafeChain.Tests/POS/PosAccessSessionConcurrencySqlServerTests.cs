using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Services.POS;
using CafeChain.Data;
using CafeChain.Infrastructure.Repositories.Operations;
using CafeChain.Models.Customers;
using CafeChain.Models.Operations;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CafeChain.Tests.POS;

[Trait("Category", "SqlServerIntegration")]
public sealed class PosAccessSessionConcurrencySqlServerTests : IAsyncLifetime
{
    private const string Database = "CafeChain_PosSessionConcurrencyTests";
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 3, 0, 0, TimeSpan.Zero);
    private static string ConnectionString => SqlServerTestConnection.Create(Database);

    public async Task InitializeAsync()
    {
        try
        {
            await using (var master = new SqlConnection(SqlServerTestConnection.MasterConnectionString()))
            {
                await master.OpenAsync();
                await using var command = master.CreateCommand();
                command.CommandText = $"IF DB_ID(N'{Database}') IS NULL CREATE DATABASE [{Database}];";
                await command.ExecuteNonQueryAsync();
            }

            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"SQL Server integration environment unavailable for POS session concurrency. Database={Database}. {ex.Message}",
                ex);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SqlServer_Concurrent_closed_shift_validation_ends_session_once_without_exception()
    {
        var identity = await SeedClosedShiftSessionAsync();
        var audit = new Mock<IWorkShiftAuditService>();
        var publisher = new Mock<IPosAccessSessionPublisher>();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var validations = Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var context = CreateContext();
            var service = new PosAccessSessionService(
                new PosAccessSessionRepository(context),
                publisher.Object,
                audit.Object,
                timeProvider: new FixedTimeProvider(Now));
            await start.Task;
            return await service.ValidateAsync(identity.PublicId, identity.JwtId);
        }).ToArray();
        start.SetResult();
        var results = await Task.WhenAll(validations);

        Assert.All(results, result =>
        {
            Assert.False(result.IsSuccess);
            Assert.Equal(WorkShiftErrorCodes.ShiftAlreadyClosed, result.ErrorCode);
        });

        await using var verify = CreateContext();
        var session = await verify.PosAccessSessions.AsNoTracking()
            .SingleAsync(x => x.PublicId == identity.PublicId);
        Assert.Equal(PosAccessSessionStatuses.WorkShiftEnded, session.Status);
        Assert.Equal("Ca làm việc đã kết thúc.", session.EndReason);
        Assert.NotNull(session.EndedAtUtc);
        audit.Verify(x => x.WriteAsync(
            "POS_SESSION_ENDED",
            session.WorkShiftId!.Value,
            session.StaffId,
            It.IsAny<object>(),
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(x => x.PublishAsync(
            It.Is<PosAccessSessionChangedDto>(notification =>
                notification.SessionId == identity.PublicId
                && notification.Status == PosAccessSessionStatuses.WorkShiftEnded),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(ConnectionString)
        .Options);

    private static async Task<(Guid PublicId, string JwtId)> SeedClosedShiftSessionAsync()
    {
        await using var context = CreateContext();
        var store = new Store
        {
            Name = "POS concurrency store",
            Address = "Test",
            Phone = "0900000000",
            Active = true,
            CreatedAt = Now.UtcDateTime
        };
        var account = new Account
        {
            Email = "pos-session-concurrency@test.local",
            PasswordHash = "x",
            Active = true,
            CreatedAt = Now.UtcDateTime
        };
        context.AddRange(store, account);
        await context.SaveChangesAsync();

        var staff = new Staff
        {
            AccountId = account.AccountId,
            StoreId = store.StoreId,
            FullName = "POS concurrency staff",
            EmployeeStatus = 2,
            Active = true,
            CreatedAt = Now.UtcDateTime
        };
        var terminal = new PosTerminal
        {
            TerminalId = "POS-CONCURRENCY-01",
            StoreId = store.StoreId,
            Name = "POS concurrency terminal",
            Active = true,
            CreatedAtUtc = Now.UtcDateTime
        };
        context.AddRange(staff, terminal);
        await context.SaveChangesAsync();

        var shift = new WorkShift
        {
            StoreId = store.StoreId,
            UserId = staff.StaffId,
            CurrentOperatorStaffId = staff.StaffId,
            StartTimeUtc = Now.AddHours(-8).UtcDateTime,
            EndTimeUtc = Now.AddMinutes(-1).UtcDateTime,
            BusinessDate = Now.UtcDateTime.Date,
            OpenContext = WorkShiftOpenContexts.Legacy,
            CloseType = WorkShiftCloseTypes.Normal,
            StartingCash = 500_000m,
            ExpectedEndingCash = 500_000m,
            ActualEndingCash = 500_000m,
            CashDiscrepancy = 0,
            Status = WorkShiftStatuses.Closed,
            PosTerminalId = terminal.TerminalId
        };
        context.WorkShifts.Add(shift);
        await context.SaveChangesAsync();

        var publicId = Guid.NewGuid();
        var jwtId = Guid.NewGuid().ToString("N");
        context.PosAccessSessions.Add(new PosAccessSession
        {
            PublicId = publicId,
            JwtId = jwtId,
            AccountId = account.AccountId,
            StaffId = staff.StaffId,
            StoreId = store.StoreId,
            TerminalId = terminal.TerminalId,
            WorkShiftId = shift.ShiftId,
            ExchangeContextId = 1,
            Status = PosAccessSessionStatuses.Active,
            IssuedAtUtc = Now.AddMinutes(-30).UtcDateTime,
            ExpiresAtUtc = Now.AddHours(4).UtcDateTime
        });
        await context.SaveChangesAsync();
        return (publicId, jwtId);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
