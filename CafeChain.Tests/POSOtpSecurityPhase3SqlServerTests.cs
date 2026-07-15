using System;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Data;
using CafeChain.Models.Staffs;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>
    /// Phase 3/4 (#140/#143) SQL boundary: current model has no Staff.PinHash;
    /// EnsureCreated schema must not expose PIN credential column under active model.
    /// Dedicated DB: CafeChain_OtpSecurityPhase3Tests.
    /// </summary>
    [Trait("Category", "SqlServerIntegration")]
    public sealed class POSOtpSecurityPhase3SqlServerTests : IAsyncLifetime
    {
        private const string Database = "CafeChain_OtpSecurityPhase3Tests";

        private static string ConnectionString => SqlServerTestConnection.Create(Database);

        private static string MasterConnectionString => SqlServerTestConnection.MasterConnectionString();

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
                    $"SQL Server integration environment unavailable for OTP Phase 3/4. Database={Database}. {ex.Message}",
                    ex);
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task SqlServer_StaffSchema_DoesNotContainPinHash_UnderCurrentModel()
        {
            Assert.Null(typeof(Staff).GetProperty("PinHash"));

            await using var ctx = CreateContext();
            var entity = ctx.Model.FindEntityType(typeof(Staff));
            Assert.NotNull(entity);
            Assert.DoesNotContain(entity!.GetProperties(), p => p.Name.Equals("PinHash", StringComparison.OrdinalIgnoreCase));

            await using var conn = ctx.Database.GetDbConnection();
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = N'Staffs' AND COLUMN_NAME = N'PinHash';";
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(0, count);
        }

        [Fact]
        public async Task SqlServer_DisabledLegacyRoutes_CannotCreateAuthorizationEvidence()
        {
            // Routes fully removed — no PIN service can write InvoiceAuditLog as auth evidence.
            await using var ctx = CreateContext();
            var before = await ctx.InvoiceAuditLogs.CountAsync();

            Assert.Null(Type.GetType("CafeChain.Application.Services.POS.SupervisorAuthService, CafeChain"));
            Assert.Null(typeof(CafeChain.Controllers.AttendanceController).GetMethod("AuthorizeBypass"));
            Assert.Null(typeof(CafeChain.Areas.Admin.Controllers.AdminPOSController).GetMethod("AuthorizeSupervisor"));

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
