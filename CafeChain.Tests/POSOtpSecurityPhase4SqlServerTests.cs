using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Services.POS;
using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Infrastructure.Repositories.Admin.POS;
using CafeChain.Models.Customers;
using CafeChain.Models.Operations;
using CafeChain.Models.Permissions;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>
    /// Phase 4 (#143) SQL proof: no PinHash column under EnsureCreated model; OTP flows work without PIN schema.
    /// Dedicated DB: CafeChain_OtpSecurityPhase4Tests.
    /// </summary>
    [Trait("Category", "SqlServerIntegration")]
    public sealed class POSOtpSecurityPhase4SqlServerTests : IAsyncLifetime
    {
        private const string Database = "CafeChain_OtpSecurityPhase4Tests";

        private static readonly OtpPayloadFingerprintService Fingerprint = new();

        private int _storeId;
        private int _requesterId;
        private int _approverId;

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
                await SeedCoreAsync(ctx);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"SQL Server integration environment unavailable for OTP Phase 4. Database={Database}. {ex.Message}",
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
            Assert.DoesNotContain(entity!.GetProperties(), p =>
                p.Name.Equals("PinHash", StringComparison.OrdinalIgnoreCase));

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
        public async Task SqlServer_OtpFlows_WorkWithoutPinSchema()
        {
            await using var ctx = CreateContext();
            var shiftId = await OpenShiftAsync(ctx, _requesterId);
            var otpRepo = new OtpChallengeRepository(ctx);
            var publicId = Guid.NewGuid();
            var reason = "SQL phase4 cash diff";
            var actual = 400_000m;
            var fingerprint = Fingerprint.BuildCashDifferenceFingerprint(
                _storeId, _requesterId, shiftId, actual, reason);

            ctx.OtpChallenges.Add(new OtpChallenge
            {
                PublicId = publicId,
                StoreId = _storeId,
                WorkShiftId = shiftId,
                RequestedByStaffId = _requesterId,
                ApproverStaffId = _approverId,
                ActionType = OtpConstants.ActionTypes.CashDifference,
                TargetType = OtpConstants.TargetTypes.Shifts,
                TargetId = shiftId,
                Reason = reason,
                PayloadFingerprint = fingerprint,
                OtpHash = BCrypt.Net.BCrypt.HashPassword("AB2C3D"),
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                Status = OtpConstants.Statuses.Approved,
                ApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                LastSentAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();

            var service = new WorkShiftService(
                new WorkShiftRepository(ctx),
                Mock.Of<IPOSOrderRepository>(),
                otpRepo,
                Fingerprint,
                NullLogger<WorkShiftService>.Instance);

            var result = await service.CloseShiftAsync(_requesterId, _storeId, new CloseShiftRequestDto
            {
                ActualEndingCash = actual,
                DiscrepancyReason = reason,
                OtpChallengePublicId = publicId
            });

            Assert.True(result.IsSuccess, result.Message);

            await using var verify = CreateContext();
            var challenge = await verify.OtpChallenges.AsNoTracking()
                .FirstAsync(c => c.PublicId == publicId);
            Assert.Equal(OtpConstants.Statuses.Used, challenge.Status);
            Assert.NotNull(challenge.UsedAt);
        }

        [Fact]
        public async Task SqlServer_DisabledLegacyRoutes_CannotCreateAuthorizationEvidence()
        {
            await using var ctx = CreateContext();
            var before = await ctx.InvoiceAuditLogs.CountAsync();

            Assert.Null(Type.GetType("CafeChain.Application.Services.POS.SupervisorAuthService, CafeChain"));
            Assert.Null(Type.GetType("CafeChain.Controllers.AttendanceController, CafeChain"));
            Assert.Null(typeof(CafeChain.Areas.Admin.Controllers.AdminPOSController).GetMethod("AuthorizeSupervisor"));

            var after = await ctx.InvoiceAuditLogs.CountAsync();
            Assert.Equal(before, after);
        }

        private async Task SeedCoreAsync(AppDbContext ctx)
        {
            var store = await ctx.Stores.FirstOrDefaultAsync(s => s.Active);
            if (store == null)
            {
                var created = new Store
                {
                    Name = "OTP Phase4 Store",
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                };
                ctx.Stores.Add(created);
                await ctx.SaveChangesAsync();
                _storeId = created.StoreId;
            }
            else
            {
                _storeId = store.StoreId;
            }

            _requesterId = await EnsureStaffAsync(ctx, RoleConstants.SalesStaff, $"cashier-p4-{Guid.NewGuid():N}@test.local");
            _approverId = await EnsureStaffAsync(ctx, RoleConstants.ShiftSupervisor, $"ss-p4-{Guid.NewGuid():N}@test.local");
        }

        private async Task<int> EnsureStaffAsync(AppDbContext ctx, string roleName, string email)
        {
            var role = await ctx.Roles.FirstAsync(r => r.Name == roleName);
            var account = new Account
            {
                Email = email,
                PasswordHash = "x",
                Active = true,
                CreatedAt = DateTime.UtcNow
            };
            ctx.Accounts.Add(account);
            await ctx.SaveChangesAsync();

            ctx.AccountRoles.Add(new AccountRole { AccountId = account.AccountId, RoleId = role.RoleId });
            var staff = new Staff
            {
                AccountId = account.AccountId,
                StoreId = _storeId,
                FullName = $"SQL Staff {email}",
                Active = true,
                CreatedAt = DateTime.UtcNow,
                StaffShifts = new List<StaffShift>()
            };
            ctx.Staffs.Add(staff);
            await ctx.SaveChangesAsync();
            return staff.StaffId;
        }

        private async Task<int> OpenShiftAsync(AppDbContext ctx, int userId)
        {
            var opens = await ctx.WorkShifts
                .Where(s => s.UserId == userId && s.StoreId == _storeId && s.Status == "Open")
                .ToListAsync();
            foreach (var o in opens)
            {
                o.Status = "Closed";
                o.EndTime = DateTime.Now;
            }

            var shift = new WorkShift
            {
                StoreId = _storeId,
                UserId = userId,
                Status = "Open",
                StartTime = DateTime.Now,
                StartingCash = 500_000m,
                ExpectedEndingCash = 500_000m
            };
            ctx.WorkShifts.Add(shift);
            await ctx.SaveChangesAsync();
            return shift.ShiftId;
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
