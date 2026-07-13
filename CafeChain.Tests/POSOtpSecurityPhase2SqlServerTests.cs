using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Attendance;
using CafeChain.Application.Interfaces.POS;
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
    /// Phase 2 (#141) SQL concurrency — CafeChain_OtpSecurityPhase2Tests.
    /// </summary>
    public sealed class POSOtpSecurityPhase2SqlServerTests : IAsyncLifetime
    {
        private const string Server = @"DESKTOP-K038H12\SQLEXPRESS";
        private const string Database = "CafeChain_OtpSecurityPhase2Tests";

        private static string ConnectionString =>
            $"Server={Server};Database={Database};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        private static string MasterConnectionString =>
            $"Server={Server};Database=master;Trusted_Connection=True;TrustServerCertificate=True";

        private int _storeId;
        private int _requesterId;
        private int _approverId;
        private readonly OtpPayloadFingerprintService _fp = new();

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
                await SeedBaseAsync(ctx);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"SQL Server integration environment unavailable for OTP Phase 2. Server={Server}, Database={Database}. {ex.Message}",
                    ex);
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task SqlServer_CloseException_ConcurrentConsume_ClosesOnce()
        {
            await using var setup = CreateContext();
            var shiftId = await OpenShiftRowAsync(setup, _requesterId);
            const decimal cash = 500_000m;
            const string reason = "SQL exception concurrent";
            var offline = new OfflineQueueSummaryDto { OfflineOrderCount = 1, EstimatedTotal = 10_000m, LocalCashTotal = 10_000m };
            var publicId = await InsertApprovedExceptionAsync(setup, shiftId, cash, reason, offline);

            var tasks = Enumerable.Range(0, 6).Select(async _ =>
            {
                await using var ctx = CreateContext();
                var service = CreateWorkShiftService(ctx);
                return await service.CloseShiftByExceptionAsync(_requesterId, _storeId, shiftId,
                    new CloseShiftExceptionRequestDto
                    {
                        ActualEndingCash = cash,
                        ExceptionReason = reason,
                        OtpChallengePublicId = publicId,
                        OfflineQueueSummary = offline
                    });
            });

            var results = await Task.WhenAll(tasks);
            Assert.Equal(1, results.Count(r => r.IsSuccess));

            await using var verify = CreateContext();
            var shift = await verify.WorkShifts.AsNoTracking().FirstAsync(s => s.ShiftId == shiftId);
            var challenge = await verify.OtpChallenges.AsNoTracking().FirstAsync(c => c.PublicId == publicId);
            Assert.Equal("Closed", shift.Status);
            Assert.True(shift.IsExceptionClosed);
            Assert.Equal(OtpConstants.Statuses.Used, challenge.Status);
        }

        [Fact]
        public async Task SqlServer_CloseException_ActionMutationAndConsume_AreAtomic()
        {
            await using var setup = CreateContext();
            var shiftId = await OpenShiftRowAsync(setup, _requesterId);
            const decimal cash = 500_000m;
            const string reason = "SQL atomic fail";
            var offline = new OfflineQueueSummaryDto { OfflineOrderCount = 1, EstimatedTotal = 1, LocalCashTotal = 1 };
            var publicId = await InsertApprovedExceptionAsync(setup, shiftId, cash, reason, offline);

            await using var ctx = CreateContext();
            var otpRepo = new OtpChallengeRepository(ctx);
            var realShift = new WorkShiftRepository(ctx);
            var shiftRepo = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            shiftRepo.Setup(r => r.GetActiveShiftAsync(_requesterId, _storeId))
                .Returns((int u, int s) => realShift.GetActiveShiftAsync(u, s));
            shiftRepo.Setup(r => r.HasOpenPosPaymentAsync(It.IsAny<int>(), _storeId)).ReturnsAsync(false);
            shiftRepo.Setup(r => r.GetTotalCashSalesAsync(It.IsAny<int>())).ReturnsAsync(0m);
            shiftRepo.Setup(r => r.UpdateShiftAsync(It.IsAny<WorkShift>()))
                .ThrowsAsync(new InvalidOperationException("Simulated SQL mutation failure"));

            var service = new WorkShiftService(
                shiftRepo.Object,
                Mock.Of<IHrAttendanceService>(),
                Mock.Of<IPOSOrderRepository>(),
                Mock.Of<ISupervisorAuthService>(),
                otpRepo,
                _fp,
                NullLogger<WorkShiftService>.Instance);

            var result = await service.CloseShiftByExceptionAsync(_requesterId, _storeId, shiftId,
                new CloseShiftExceptionRequestDto
                {
                    ActualEndingCash = cash,
                    ExceptionReason = reason,
                    OtpChallengePublicId = publicId,
                    OfflineQueueSummary = offline
                });
            Assert.False(result.IsSuccess);

            await using var verify = CreateContext();
            var shift = await verify.WorkShifts.AsNoTracking().FirstAsync(s => s.ShiftId == shiftId);
            var challenge = await verify.OtpChallenges.AsNoTracking().FirstAsync(c => c.PublicId == publicId);
            Assert.Equal("Open", shift.Status);
            Assert.Equal(OtpConstants.Statuses.Approved, challenge.Status);
        }

        [Fact]
        public async Task SqlServer_CloseException_PayloadMismatch_CannotConsume()
        {
            await using var setup = CreateContext();
            var shiftId = await OpenShiftRowAsync(setup, _requesterId);
            var offline = new OfflineQueueSummaryDto { OfflineOrderCount = 1, EstimatedTotal = 1, LocalCashTotal = 1 };
            var publicId = await InsertApprovedExceptionAsync(setup, shiftId, 500_000m, "reason A", offline);

            await using var ctx = CreateContext();
            var service = CreateWorkShiftService(ctx);
            var result = await service.CloseShiftByExceptionAsync(_requesterId, _storeId, shiftId,
                new CloseShiftExceptionRequestDto
                {
                    ActualEndingCash = 430_000m,
                    DiscrepancyReason = "Đổi cash fingerprint",
                    ExceptionReason = "reason A",
                    OtpChallengePublicId = publicId,
                    OfflineQueueSummary = offline
                });

            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.ErrorCodes.PayloadMismatch, result.ErrorCode);
        }

        [Fact]
        public async Task SqlServer_OpenShiftLate_ConcurrentConsume_CreatesOneShift()
        {
            await using var setup = CreateContext();
            await EnsureLateScheduleAsync(setup, _requesterId);
            // Close any open shifts for requester
            await CloseOpenShiftsAsync(setup, _requesterId);

            const decimal cash = 500_000m;
            const string reason = "SQL late concurrent";
            var scheduled = await ResolveScheduledAsync(setup, _requesterId);
            var publicId = await InsertApprovedOpenLateAsync(setup, cash, reason, scheduled);

            var tasks = Enumerable.Range(0, 6).Select(async _ =>
            {
                await using var ctx = CreateContext();
                var service = CreateWorkShiftService(ctx, hrOk: true);
                return await service.OpenShiftAsync(_requesterId, _storeId, new OpenShiftRequestDto
                {
                    StartingCash = cash,
                    LateOpeningReason = reason,
                    OtpChallengePublicId = publicId
                });
            });

            var results = await Task.WhenAll(tasks);
            var success = results.Count(r => r.IsSuccess);
            Assert.Equal(1, success);

            await using var verify = CreateContext();
            var opens = await verify.WorkShifts.CountAsync(s =>
                s.UserId == _requesterId && s.StoreId == _storeId && s.Status == "Open");
            Assert.Equal(1, opens);
            var challenge = await verify.OtpChallenges.AsNoTracking().FirstAsync(c => c.PublicId == publicId);
            Assert.Equal(OtpConstants.Statuses.Used, challenge.Status);
        }

        [Fact]
        public async Task SqlServer_OpenShiftLate_ActionMutationAndConsume_AreAtomic()
        {
            await using var setup = CreateContext();
            await EnsureLateScheduleAsync(setup, _requesterId);
            await CloseOpenShiftsAsync(setup, _requesterId);

            const decimal cash = 500_000m;
            const string reason = "SQL late atomic";
            var scheduled = await ResolveScheduledAsync(setup, _requesterId);
            var publicId = await InsertApprovedOpenLateAsync(setup, cash, reason, scheduled);

            await using var ctx = CreateContext();
            var otpRepo = new OtpChallengeRepository(ctx);
            var realShift = new WorkShiftRepository(ctx);
            var shiftRepo = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            shiftRepo.Setup(r => r.GetActiveShiftAsync(_requesterId, _storeId)).ReturnsAsync((WorkShift?)null);
            shiftRepo.Setup(r => r.GetTodayStaffShiftAsync(_requesterId))
                .Returns(() => realShift.GetTodayStaffShiftAsync(_requesterId));
            shiftRepo.Setup(r => r.EnsurePosTerminalAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            shiftRepo.Setup(r => r.CreateShiftAsync(It.IsAny<WorkShift>()))
                .ThrowsAsync(new InvalidOperationException("Simulated open fail"));

            var hr = new Mock<IHrAttendanceService>();
            hr.Setup(h => h.VerifyRecentCheckInAsync(_requesterId, _storeId)).ReturnsAsync(true);

            var service = new WorkShiftService(
                shiftRepo.Object,
                hr.Object,
                Mock.Of<IPOSOrderRepository>(),
                Mock.Of<ISupervisorAuthService>(),
                otpRepo,
                _fp,
                NullLogger<WorkShiftService>.Instance);

            var result = await service.OpenShiftAsync(_requesterId, _storeId, new OpenShiftRequestDto
            {
                StartingCash = cash,
                LateOpeningReason = reason,
                OtpChallengePublicId = publicId
            });
            Assert.False(result.IsSuccess);

            await using var verify = CreateContext();
            var challenge = await verify.OtpChallenges.AsNoTracking().FirstAsync(c => c.PublicId == publicId);
            Assert.Equal(OtpConstants.Statuses.Approved, challenge.Status);
        }

        [Fact]
        public async Task SqlServer_ShiftOtp_ApproverDisabledBeforeConsume_IsRejected()
        {
            await using var setup = CreateContext();
            var shiftId = await OpenShiftRowAsync(setup, _requesterId);
            var offline = new OfflineQueueSummaryDto { OfflineOrderCount = 1, EstimatedTotal = 1, LocalCashTotal = 1 };
            var publicId = await InsertApprovedExceptionAsync(setup, shiftId, 500_000m, "disabled", offline);

            var approver = await setup.Staffs.FirstAsync(s => s.StaffId == _approverId);
            approver.Active = false;
            await setup.SaveChangesAsync();

            await using var ctx = CreateContext();
            var service = CreateWorkShiftService(ctx);
            var result = await service.CloseShiftByExceptionAsync(_requesterId, _storeId, shiftId,
                new CloseShiftExceptionRequestDto
                {
                    ActualEndingCash = 500_000m,
                    ExceptionReason = "disabled",
                    OtpChallengePublicId = publicId,
                    OfflineQueueSummary = offline
                });

            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.ErrorCodes.ApproverNoLongerEligible, result.ErrorCode);

            await using var restore = CreateContext();
            var a = await restore.Staffs.FirstAsync(s => s.StaffId == _approverId);
            a.Active = true;
            await restore.SaveChangesAsync();
        }

        [Fact]
        public async Task SqlServer_ShiftOtp_SelfApproval_CannotWinRace()
        {
            await using var setup = CreateContext();
            var shiftId = await OpenShiftRowAsync(setup, _requesterId);
            var offline = new OfflineQueueSummaryDto { OfflineOrderCount = 1, EstimatedTotal = 1, LocalCashTotal = 1 };
            var publicId = await InsertApprovedExceptionAsync(
                setup, shiftId, 500_000m, "self", offline, approverId: _requesterId);

            var tasks = Enumerable.Range(0, 4).Select(async _ =>
            {
                await using var ctx = CreateContext();
                var service = CreateWorkShiftService(ctx);
                return await service.CloseShiftByExceptionAsync(_requesterId, _storeId, shiftId,
                    new CloseShiftExceptionRequestDto
                    {
                        ActualEndingCash = 500_000m,
                        ExceptionReason = "self",
                        OtpChallengePublicId = publicId,
                        OfflineQueueSummary = offline
                    });
            });

            var results = await Task.WhenAll(tasks);
            Assert.All(results, r =>
            {
                Assert.False(r.IsSuccess);
                Assert.Equal(OtpConstants.ErrorCodes.NoEligibleApprover, r.ErrorCode);
            });
        }

        [Fact]
        public async Task SqlServer_ShiftOtp_InvoiceAuditLogCannotAuthorizeWithoutChallenge()
        {
            await using var setup = CreateContext();
            await EnsureLateScheduleAsync(setup, _requesterId);
            await CloseOpenShiftsAsync(setup, _requesterId);

            // Seed a recent PIN-style audit (should not authorize late open).
            setup.InvoiceAuditLogs.Add(new CafeChain.Models.Orders.InvoiceAuditLog
            {
                CashierId = _requesterId,
                SupervisorId = _approverId,
                ActionName = "OPEN_SHIFT_LATE",
                Reason = "legacy pin",
                CreatedAt = DateTime.Now
            });
            await setup.SaveChangesAsync();

            await using var ctx = CreateContext();
            var service = CreateWorkShiftService(ctx, hrOk: true);
            var result = await service.OpenShiftAsync(_requesterId, _storeId, new OpenShiftRequestDto
            {
                StartingCash = 500_000m
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.ErrorCodes.LateOpeningRequiresOtp, result.ErrorCode);
        }

        // ---- infra ----

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;
            return new AppDbContext(options);
        }

        private async Task SeedBaseAsync(AppDbContext ctx)
        {
            var store = await ctx.Stores.AsNoTracking().OrderBy(s => s.StoreId).FirstOrDefaultAsync();
            if (store == null)
            {
                var created = new Store { Name = "OTP P2 SQL", Active = true, CreatedAt = DateTime.UtcNow };
                ctx.Stores.Add(created);
                await ctx.SaveChangesAsync();
                _storeId = created.StoreId;
            }
            else _storeId = store.StoreId;

            _requesterId = await EnsureStaffAsync(ctx, RoleConstants.SalesStaff, $"p2-cashier-{Guid.NewGuid():N}@test.local");
            _approverId = await EnsureStaffAsync(ctx, RoleConstants.ShiftSupervisor, $"p2-ss-{Guid.NewGuid():N}@test.local");
            await EnsureStaffAsync(ctx, RoleConstants.StoreManager, $"p2-sm-{Guid.NewGuid():N}@test.local");
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
                FullName = email,
                Active = true,
                CreatedAt = DateTime.UtcNow,
                BaseSalary = 0,
                StaffShifts = new List<StaffShift>()
            };
            ctx.Staffs.Add(staff);
            await ctx.SaveChangesAsync();
            return staff.StaffId;
        }

        private async Task EnsureLateScheduleAsync(AppDbContext ctx, int staffId)
        {
            if (!await ctx.Shifts.AnyAsync(s => s.StoreId == _storeId))
            {
                ctx.Shifts.Add(new Shift
                {
                    Name = "Ca SQL P2",
                    StartTime = TimeSpan.FromHours(0),
                    EndTime = TimeSpan.FromHours(8),
                    Active = true,
                    StoreId = _storeId
                });
                await ctx.SaveChangesAsync();
            }

            var shift = await ctx.Shifts.Where(s => s.StoreId == _storeId).OrderBy(s => s.ShiftId).FirstAsync();
            if (!await ctx.StaffShifts.AnyAsync(ss => ss.StaffId == staffId && ss.WorkDate.Date == DateTime.Today))
            {
                ctx.StaffShifts.Add(new StaffShift
                {
                    StaffId = staffId,
                    ShiftId = shift.ShiftId,
                    WorkDate = DateTime.Today,
                    StatusId = 1
                });
                await ctx.SaveChangesAsync();
            }
        }

        private async Task<string> ResolveScheduledAsync(AppDbContext ctx, int staffId)
        {
            var ss = await ctx.StaffShifts.Include(s => s.Shift)
                .FirstOrDefaultAsync(s => s.StaffId == staffId && s.WorkDate == DateTime.Today);
            if (ss?.Shift == null) return "none";
            return DateTime.Today.Add(ss.Shift.StartTime)
                .ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private async Task CloseOpenShiftsAsync(AppDbContext ctx, int userId)
        {
            var opens = await ctx.WorkShifts
                .Where(s => s.UserId == userId && s.StoreId == _storeId && s.Status == "Open")
                .ToListAsync();
            foreach (var o in opens)
            {
                o.Status = "Closed";
                o.EndTime = DateTime.Now;
            }
            await ctx.SaveChangesAsync();
        }

        private async Task<int> OpenShiftRowAsync(AppDbContext ctx, int userId)
        {
            await CloseOpenShiftsAsync(ctx, userId);
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

        private async Task<Guid> InsertApprovedExceptionAsync(
            AppDbContext ctx,
            int shiftId,
            decimal cash,
            string reason,
            OfflineQueueSummaryDto offline,
            int? approverId = null)
        {
            var publicId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var approver = approverId ?? _approverId;
            ctx.OtpChallenges.Add(new OtpChallenge
            {
                PublicId = publicId,
                StoreId = _storeId,
                WorkShiftId = shiftId,
                RequestedByStaffId = _requesterId,
                ApproverStaffId = approver,
                ActionType = OtpConstants.ActionTypes.CloseShiftException,
                TargetType = OtpConstants.TargetTypes.Shifts,
                TargetId = shiftId,
                Reason = reason,
                PayloadFingerprint = _fp.BuildCloseShiftExceptionFingerprint(
                    _storeId, _requesterId, shiftId, cash, reason, null, offline),
                OtpHash = BCrypt.Net.BCrypt.HashPassword("APPR2V"),
                ExpiresAt = now.AddMinutes(5),
                LastSentAt = now,
                CreatedAt = now,
                ApprovedAt = now,
                Status = OtpConstants.Statuses.Approved
            });
            await ctx.SaveChangesAsync();
            return publicId;
        }

        private async Task<Guid> InsertApprovedOpenLateAsync(
            AppDbContext ctx, decimal cash, string reason, string scheduled)
        {
            var publicId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            ctx.OtpChallenges.Add(new OtpChallenge
            {
                PublicId = publicId,
                StoreId = _storeId,
                WorkShiftId = null,
                RequestedByStaffId = _requesterId,
                ApproverStaffId = _approverId,
                ActionType = OtpConstants.ActionTypes.OpenShiftLate,
                TargetType = OtpConstants.TargetTypes.Shifts,
                TargetId = _requesterId,
                Reason = reason,
                PayloadFingerprint = _fp.BuildOpenShiftLateFingerprint(
                    _storeId, _requesterId, cash, reason, scheduled),
                OtpHash = BCrypt.Net.BCrypt.HashPassword("APPR2V"),
                ExpiresAt = now.AddMinutes(5),
                LastSentAt = now,
                CreatedAt = now,
                ApprovedAt = now,
                Status = OtpConstants.Statuses.Approved
            });
            await ctx.SaveChangesAsync();
            return publicId;
        }

        private WorkShiftService CreateWorkShiftService(AppDbContext ctx, bool hrOk = true)
        {
            var hr = new Mock<IHrAttendanceService>();
            hr.Setup(h => h.VerifyRecentCheckInAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(hrOk);
            return new WorkShiftService(
                new WorkShiftRepository(ctx),
                hr.Object,
                new POSOrderRepository(ctx),
                Mock.Of<ISupervisorAuthService>(),
                new OtpChallengeRepository(ctx),
                _fp,
                NullLogger<WorkShiftService>.Instance);
        }
    }
}
