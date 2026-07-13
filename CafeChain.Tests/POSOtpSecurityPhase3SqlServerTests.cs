using System;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.Services.POS;
using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Orders;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>
    /// Phase 3 (#140) SQL boundary: disabled PIN endpoints must not create authorization evidence.
    /// Dedicated DB: CafeChain_OtpSecurityPhase3Tests.
    /// </summary>
    public sealed class POSOtpSecurityPhase3SqlServerTests : IAsyncLifetime
    {
        private const string Server = @"DESKTOP-K038H12\SQLEXPRESS";
        private const string Database = "CafeChain_OtpSecurityPhase3Tests";

        private static string ConnectionString =>
            $"Server={Server};Database={Database};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        private static string MasterConnectionString =>
            $"Server={Server};Database=master;Trusted_Connection=True;TrustServerCertificate=True";

        public async Task InitializeAsync()
        {
            try
            {
                await using (var master = new SqlConnection(MasterConnectionString))
                {
                    await master.OpenAsync();
                    await using var cmd = master.CreateCommand();
                    cmd.CommandText = $@"
IF DB_ID(N'{Database}') IS NULL
    CREATE DATABASE [{Database}];";
                    await cmd.ExecuteNonQueryAsync();
                }

                await using var ctx = CreateContext();
                await ctx.Database.EnsureDeletedAsync();
                await ctx.Database.EnsureCreatedAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"SQL Server integration environment unavailable for OTP Phase 3. Server={Server}, Database={Database}. {ex.Message}",
                    ex);
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task SqlServer_DisabledPinEndpoint_CannotCreateAuthorizationEvidence()
        {
            await using var ctx = CreateContext();
            var before = await ctx.InvoiceAuditLogs.CountAsync();

            // Even if a repository were wired, AuthorizePinAsync must not call CreateAuditLog.
            var repo = new Mock<ISupervisorRepository>(MockBehavior.Strict);
            var service = new SupervisorAuthService(repo.Object, new MemoryCache(new MemoryCacheOptions()));

            var tasks = Enumerable.Range(0, 8).Select(async i =>
            {
                var r = await service.AuthorizePinAsync(
                    "1234", cashierId: 1, storeId: 1,
                    actionName: "VOID_INVOICE", targetId: i, reason: "race");
                Assert.False(r.IsSuccess);
                Assert.Equal(OtpConstants.ErrorCodes.FeatureNotAvailable, r.ErrorCode);
                return r;
            });

            await Task.WhenAll(tasks);
            repo.Verify(r => r.CreateAuditLogAsync(It.IsAny<InvoiceAuditLog>()), Times.Never);

            await using var verify = CreateContext();
            var after = await verify.InvoiceAuditLogs.CountAsync();
            Assert.Equal(before, after);
        }

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            return new AppDbContext(options);
        }
    }
}
