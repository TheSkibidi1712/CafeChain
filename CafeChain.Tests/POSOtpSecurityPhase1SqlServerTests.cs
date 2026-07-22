//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using CafeChain.Application.Constants;
//using CafeChain.Application.DTOs.POS;
//using CafeChain.Application.Interfaces.Accounts;
//using CafeChain.Application.Interfaces.POS;
//using CafeChain.Application.Services.POS;
//using CafeChain.Data;
//using CafeChain.Infrastructure.Interfaces.Admin.POS;
//using CafeChain.Infrastructure.Repositories.Admin.POS;
//using CafeChain.Models.Customers;
//using CafeChain.Models.Operations;
//using CafeChain.Models.Permissions;
//using CafeChain.Models.Staffs;
//using CafeChain.Models.Stores;
//using Microsoft.AspNetCore.Hosting;
//using Microsoft.Data.SqlClient;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging.Abstractions;
//using Moq;
//using Xunit;

//namespace CafeChain.Tests.POS
//{
//    /// <summary>
//    /// Phase 1 (#142) SQL Server concurrency proofs.
//    /// Dedicated DB: CafeChain_OtpSecurityPhase1Tests (EnsureDeleted → EnsureCreated).
//    /// Critical tests must not skip when SQL Server is available.
//    /// </summary>
//    [Trait("Category", "SqlServerIntegration")]
//    public sealed class POSOtpSecurityPhase1SqlServerTests : IAsyncLifetime
//    {
//        private const string Database = "CafeChain_OtpSecurityPhase1Tests";

//        private static string ConnectionString => SqlServerTestConnection.Create(Database);

//        private static string MasterConnectionString => SqlServerTestConnection.MasterConnectionString();

//        private int _storeId;
//        private int _requesterId;
//        private int _approverId;

//        private readonly OtpPayloadFingerprintService _fingerprint = new();
//        private readonly OtpCodeGenerator _codeGenerator = new();

//        public async Task InitializeAsync()
//        {
//            try
//            {
//                await using (var master = new SqlConnection(MasterConnectionString))
//                {
//                    await master.OpenAsync();
//                    await using var cmd = master.CreateCommand();
//                    cmd.CommandText = $@"
//IF DB_ID(N'{Database}') IS NULL
//    CREATE DATABASE [{Database}];";
//                    await cmd.ExecuteNonQueryAsync();
//                }

//                await using var ctx = CreateContext();
//                await ctx.Database.EnsureDeletedAsync();
//                await ctx.Database.EnsureCreatedAsync();
//                await SeedBaseAsync(ctx);
//            }
//            catch (Exception ex)
//            {
//                throw new InvalidOperationException(
//                    $"SQL Server integration environment unavailable for OTP Phase 1. Database={Database}. {ex.Message}",
//                    ex);
//            }
//        }

//        public Task DisposeAsync() => Task.CompletedTask;

//        [Fact]
//        public async Task SqlServer_Otp_ConcurrentRequest_CreatesOneActiveChallenge()
//        {
//            await using var setup = CreateContext();
//            var shiftId = await OpenShiftAsync(setup, _requesterId);

//            var tasks = Enumerable.Range(0, 8).Select(async _ =>
//            {
//                await using var ctx = CreateContext();
//                var service = CreateApprovalService(ctx, "AB23CD");
//                return await service.RequestOtpAsync(NewRequest(shiftId), _requesterId, _storeId);
//            });

//            var results = await Task.WhenAll(tasks);
//            Assert.All(results, r => Assert.True(r.IsSuccess, r.Message));

//            await using var verify = CreateContext();
//            var active = await verify.OtpChallenges
//                .Where(c =>
//                    c.StoreId == _storeId &&
//                    c.RequestedByStaffId == _requesterId &&
//                    c.ActionType == OtpConstants.ActionTypes.CashDifference &&
//                    c.TargetId == shiftId &&
//                    (c.Status == OtpConstants.Statuses.Pending || c.Status == OtpConstants.Statuses.Approved))
//                .ToListAsync();

//            Assert.Equal(1, active.Count);
//            var distinctPublicIds = results.Select(r => r.Data!.OtpChallengePublicId).Distinct().Count();
//            Assert.Equal(1, distinctPublicIds);
//        }

//        [Fact]
//        public async Task SqlServer_Otp_ConcurrentVerify_AllowsOneWinner()
//        {
//            await using var setup = CreateContext();
//            var shiftId = await OpenShiftAsync(setup, _requesterId);
//            const string code = "VER2FY"; // alphabet-safe (no O/0/I/1)
//            var publicId = await InsertPendingAsync(setup, shiftId, code);

//            var tasks = Enumerable.Range(0, 6).Select(async _ =>
//            {
//                await using var ctx = CreateContext();
//                var service = CreateApprovalService(ctx, "ZZZZZZ");
//                return await service.VerifyOtpAsync(new OtpVerifyDto
//                {
//                    OtpChallengePublicId = publicId,
//                    OtpCode = code
//                });
//            });

//            var results = await Task.WhenAll(tasks);
//            var successes = results.Count(r => r.IsSuccess);
//            Assert.Equal(1, successes);

//            await using var verify = CreateContext();
//            var challenge = await verify.OtpChallenges.AsNoTracking().FirstAsync(c => c.PublicId == publicId);
//            Assert.Equal(OtpConstants.Statuses.Approved, challenge.Status);
//        }

//        [Fact]
//        public async Task SqlServer_Otp_ResendVsVerify_SerializesSafely()
//        {
//            await using var setup = CreateContext();
//            var shiftId = await OpenShiftAsync(setup, _requesterId);
//            const string oldCode = "XLD234"; // alphabet-safe
//            const string newCode = "NEW234";
//            var publicId = await InsertPendingAsync(
//                setup, shiftId, oldCode, lastSentAt: DateTime.UtcNow.AddSeconds(-120));

//            var verifyTask = Task.Run(async () =>
//            {
//                await using var ctx = CreateContext();
//                var service = CreateApprovalService(ctx, "ZZZZZZ");
//                return await service.VerifyOtpAsync(new OtpVerifyDto
//                {
//                    OtpChallengePublicId = publicId,
//                    OtpCode = oldCode
//                });
//            });

//            var resendTask = Task.Run(async () =>
//            {
//                await using var ctx = CreateContext();
//                var service = CreateApprovalService(ctx, newCode);
//                return await service.ResendOtpAsync(new OtpResendDto { OtpChallengePublicId = publicId });
//            });

//            await Task.WhenAll(verifyTask, resendTask);

//            await using var check = CreateContext();
//            var challenge = await check.OtpChallenges.AsNoTracking().FirstAsync(c => c.PublicId == publicId);

//            // Serialized: terminal state is either Approved once or Pending with a single valid hash.
//            Assert.True(
//                challenge.Status == OtpConstants.Statuses.Approved
//                || challenge.Status == OtpConstants.Statuses.Pending);

//            if (challenge.Status == OtpConstants.Statuses.Approved)
//            {
//                Assert.NotNull(challenge.ApprovedAt);
//            }
//            else
//            {
//                var oldStillValid = BCrypt.Net.BCrypt.Verify(oldCode, challenge.OtpHash);
//                var newValid = BCrypt.Net.BCrypt.Verify(newCode, challenge.OtpHash);
//                Assert.True(oldStillValid ^ newValid);
//            }
//        }

//        [Fact]
//        public async Task SqlServer_Otp_ConcurrentConsume_AllowsOneShiftClose()
//        {
//            await using var setup = CreateContext();
//            var shiftId = await OpenShiftAsync(setup, _requesterId);
//            const decimal cash = 440_000m;
//            const string reason = "Thiếu tiền mặt SQL concurrent";
//            var publicId = await InsertApprovedAsync(setup, shiftId, cash, reason);

//            var tasks = Enumerable.Range(0, 6).Select(async _ =>
//            {
//                await using var ctx = CreateContext();
//                var service = CreateWorkShiftService(ctx);
//                return await service.CloseShiftAsync(_requesterId, _storeId, new CloseShiftRequestDto
//                {
//                    ActualEndingCash = cash,
//                    DiscrepancyReason = reason,
//                    OtpChallengePublicId = publicId
//                });
//            });

//            var results = await Task.WhenAll(tasks);
//            var successCount = results.Count(r => r.IsSuccess);
//            Assert.Equal(1, successCount);

//            await using var verify = CreateContext();
//            var shift = await verify.WorkShifts.AsNoTracking().FirstAsync(s => s.ShiftId == shiftId);
//            var challenge = await verify.OtpChallenges.AsNoTracking().FirstAsync(c => c.PublicId == publicId);
//            Assert.Equal("Closed", shift.Status);
//            Assert.Equal(OtpConstants.Statuses.Used, challenge.Status);
//        }

//        [Fact]
//        public async Task SqlServer_Otp_ActionMutationAndConsume_AreAtomic()
//        {
//            await using var setup = CreateContext();
//            var shiftId = await OpenShiftAsync(setup, _requesterId);
//            const decimal cash = 440_000m;
//            const string reason = "Atomic fail path";
//            var publicId = await InsertApprovedAsync(setup, shiftId, cash, reason);

//            await using var ctx = CreateContext();
//            var otpRepo = new OtpChallengeRepository(ctx);
//            var realShift = new WorkShiftRepository(ctx);
//            var shiftRepo = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
//            shiftRepo.Setup(r => r.GetActiveShiftAsync(_requesterId, _storeId))
//                .Returns((int u, int s) => realShift.GetActiveShiftAsync(u, s));
//            shiftRepo.Setup(r => r.HasOpenPosPaymentAsync(It.IsAny<int>(), _storeId)).ReturnsAsync(false);
//            shiftRepo.Setup(r => r.GetTotalCashSalesAsync(It.IsAny<int>())).ReturnsAsync(0m);
//            shiftRepo.Setup(r => r.UpdateShiftAsync(It.IsAny<WorkShift>()))
//                .ThrowsAsync(new InvalidOperationException("Simulated SQL mutation failure"));

//            var service = new WorkShiftService(
//                shiftRepo.Object,
//                Mock.Of<IPOSOrderRepository>(),
//                otpRepo,
//                _fingerprint,
//                NullLogger<WorkShiftService>.Instance);

//            var result = await service.CloseShiftAsync(_requesterId, _storeId, new CloseShiftRequestDto
//            {
//                ActualEndingCash = cash,
//                DiscrepancyReason = reason,
//                OtpChallengePublicId = publicId
//            });
//            Assert.False(result.IsSuccess);

//            await using var verify = CreateContext();
//            var shift = await verify.WorkShifts.AsNoTracking().FirstAsync(s => s.ShiftId == shiftId);
//            var challenge = await verify.OtpChallenges.AsNoTracking().FirstAsync(c => c.PublicId == publicId);
//            Assert.Equal("Open", shift.Status);
//            Assert.Equal(OtpConstants.Statuses.Approved, challenge.Status);
//            Assert.Null(challenge.UsedAt);
//        }

//        [Fact]
//        public async Task SqlServer_Otp_ApproverDisabledBeforeConsume_IsRejected()
//        {
//            await using var setup = CreateContext();
//            var shiftId = await OpenShiftAsync(setup, _requesterId);
//            const decimal cash = 440_000m;
//            const string reason = "Approver disabled";
//            var publicId = await InsertApprovedAsync(setup, shiftId, cash, reason);

//            var approver = await setup.Staffs.FirstAsync(s => s.StaffId == _approverId);
//            approver.Active = false;
//            await setup.SaveChangesAsync();

//            await using var ctx = CreateContext();
//            var service = CreateWorkShiftService(ctx);
//            var result = await service.CloseShiftAsync(_requesterId, _storeId, new CloseShiftRequestDto
//            {
//                ActualEndingCash = cash,
//                DiscrepancyReason = reason,
//                OtpChallengePublicId = publicId
//            });

//            Assert.False(result.IsSuccess);
//            Assert.Equal(OtpConstants.ErrorCodes.ApproverNoLongerEligible, result.ErrorCode);

//            // Restore for other tests sharing DB instance after EnsureCreated once
//            await using var restore = CreateContext();
//            var a = await restore.Staffs.FirstAsync(s => s.StaffId == _approverId);
//            a.Active = true;
//            await restore.SaveChangesAsync();
//        }

//        [Fact]
//        public async Task SqlServer_Otp_SelfApproval_CannotWinRace()
//        {
//            await using var setup = CreateContext();
//            var shiftId = await OpenShiftAsync(setup, _requesterId);
//            const decimal cash = 440_000m;
//            const string reason = "Self approval race";
//            // Malicious challenge: approver == requester
//            var publicId = await InsertApprovedAsync(setup, shiftId, cash, reason, approverId: _requesterId);

//            var tasks = Enumerable.Range(0, 4).Select(async _ =>
//            {
//                await using var ctx = CreateContext();
//                var service = CreateWorkShiftService(ctx);
//                return await service.CloseShiftAsync(_requesterId, _storeId, new CloseShiftRequestDto
//                {
//                    ActualEndingCash = cash,
//                    DiscrepancyReason = reason,
//                    OtpChallengePublicId = publicId
//                });
//            });

//            var results = await Task.WhenAll(tasks);
//            Assert.All(results, r =>
//            {
//                Assert.False(r.IsSuccess);
//                Assert.Equal(OtpConstants.ErrorCodes.NoEligibleApprover, r.ErrorCode);
//            });

//            await using var verify = CreateContext();
//            var shift = await verify.WorkShifts.AsNoTracking().FirstAsync(s => s.ShiftId == shiftId);
//            var challenge = await verify.OtpChallenges.AsNoTracking().FirstAsync(c => c.PublicId == publicId);
//            Assert.Equal("Open", shift.Status);
//            Assert.NotEqual(OtpConstants.Statuses.Used, challenge.Status);
//        }

//        // -----------------------------------------------------------------
//        // Infrastructure
//        // -----------------------------------------------------------------

//        private static AppDbContext CreateContext()
//        {
//            var options = new DbContextOptionsBuilder<AppDbContext>()
//                .UseSqlServer(ConnectionString)
//                .Options;
//            return new AppDbContext(options);
//        }

//        private async Task SeedBaseAsync(AppDbContext ctx)
//        {
//            // Prefer HasData store if present; otherwise create without explicit identity.
//            var store = await ctx.Stores.AsNoTracking().OrderBy(s => s.StoreId).FirstOrDefaultAsync();
//            if (store == null)
//            {
//                var created = new Store
//                {
//                    Name = "OTP SQL Store",
//                    Active = true,
//                    CreatedAt = DateTime.UtcNow
//                };
//                ctx.Stores.Add(created);
//                await ctx.SaveChangesAsync();
//                _storeId = created.StoreId;
//            }
//            else
//            {
//                _storeId = store.StoreId;
//            }

//            _requesterId = await EnsureStaffAsync(ctx, RoleConstants.SalesStaff, $"cashier-sql-{Guid.NewGuid():N}@test.local");
//            _approverId = await EnsureStaffAsync(ctx, RoleConstants.ShiftSupervisor, $"ss-sql-{Guid.NewGuid():N}@test.local");
//            // Extra manager for concurrent request tests when requester is also eligible
//            await EnsureStaffAsync(ctx, RoleConstants.StoreManager, $"sm-sql-{Guid.NewGuid():N}@test.local");
//        }

//        private async Task<int> EnsureStaffAsync(AppDbContext ctx, string roleName, string email)
//        {
//            var role = await ctx.Roles.FirstAsync(r => r.Name == roleName);
//            var account = new Account
//            {
//                Email = email,
//                PasswordHash = "x",
//                Active = true,
//                CreatedAt = DateTime.UtcNow
//            };
//            ctx.Accounts.Add(account);
//            await ctx.SaveChangesAsync();

//            ctx.AccountRoles.Add(new AccountRole { AccountId = account.AccountId, RoleId = role.RoleId });
//            var staff = new Staff
//            {
//                AccountId = account.AccountId,
//                StoreId = _storeId,
//                FullName = $"SQL Staff {email}",
//                Active = true,
//                CreatedAt = DateTime.UtcNow,
//                StaffShifts = new List<StaffShift>()
//            };
//            ctx.Staffs.Add(staff);
//            await ctx.SaveChangesAsync();
//            return staff.StaffId;
//        }

//        private async Task<int> OpenShiftAsync(AppDbContext ctx, int userId)
//        {
//            // Close any existing open shifts for isolation between tests
//            var opens = await ctx.WorkShifts
//                .Where(s => s.UserId == userId && s.StoreId == _storeId && s.Status == "Open")
//                .ToListAsync();
//            foreach (var o in opens)
//            {
//                o.Status = "Closed";
//                o.EndTime = DateTime.Now;
//            }

//            var shift = new WorkShift
//            {
//                StoreId = _storeId,
//                UserId = userId,
//                Status = "Open",
//                StartTime = DateTime.Now,
//                StartingCash = 500_000m,
//                ExpectedEndingCash = 500_000m
//            };
//            ctx.WorkShifts.Add(shift);
//            await ctx.SaveChangesAsync();
//            return shift.ShiftId;
//        }

//        private async Task<Guid> InsertPendingAsync(
//            AppDbContext ctx,
//            int shiftId,
//            string code,
//            DateTime? lastSentAt = null)
//        {
//            var now = DateTime.UtcNow;
//            var publicId = Guid.NewGuid();
//            var reason = "SQL pending";
//            ctx.OtpChallenges.Add(new OtpChallenge
//            {
//                PublicId = publicId,
//                StoreId = _storeId,
//                WorkShiftId = shiftId,
//                RequestedByStaffId = _requesterId,
//                ApproverStaffId = _approverId,
//                ActionType = OtpConstants.ActionTypes.CashDifference,
//                TargetType = OtpConstants.TargetTypes.Shifts,
//                TargetId = shiftId,
//                Reason = reason,
//                PayloadFingerprint = _fingerprint.BuildCashDifferenceFingerprint(
//                    _storeId, _requesterId, shiftId, 440_000m, reason),
//                OtpHash = BCrypt.Net.BCrypt.HashPassword(code),
//                ExpiresAt = now.AddMinutes(5),
//                LastSentAt = lastSentAt ?? now,
//                CreatedAt = now,
//                Status = OtpConstants.Statuses.Pending
//            });
//            await ctx.SaveChangesAsync();
//            return publicId;
//        }

//        private async Task<Guid> InsertApprovedAsync(
//            AppDbContext ctx,
//            int shiftId,
//            decimal cash,
//            string reason,
//            int? approverId = null)
//        {
//            var now = DateTime.UtcNow;
//            var publicId = Guid.NewGuid();
//            var approver = approverId ?? _approverId;
//            ctx.OtpChallenges.Add(new OtpChallenge
//            {
//                PublicId = publicId,
//                StoreId = _storeId,
//                WorkShiftId = shiftId,
//                RequestedByStaffId = _requesterId,
//                ApproverStaffId = approver,
//                ActionType = OtpConstants.ActionTypes.CashDifference,
//                TargetType = OtpConstants.TargetTypes.Shifts,
//                TargetId = shiftId,
//                Reason = reason,
//                PayloadFingerprint = _fingerprint.BuildCashDifferenceFingerprint(
//                    _storeId, _requesterId, shiftId, cash, reason),
//                OtpHash = BCrypt.Net.BCrypt.HashPassword("APPR2V"),
//                ExpiresAt = now.AddMinutes(5),
//                LastSentAt = now,
//                CreatedAt = now,
//                ApprovedAt = now,
//                Status = OtpConstants.Statuses.Approved
//            });
//            await ctx.SaveChangesAsync();
//            return publicId;
//        }

//        private static OtpRequestDto NewRequest(int shiftId) => new()
//        {
//            ActionType = OtpConstants.ActionTypes.CashDifference,
//            TargetType = OtpConstants.TargetTypes.Shifts,
//            TargetId = shiftId,
//            WorkShiftId = shiftId,
//            ActualEndingCash = 440_000m,
//            Reason = "SQL concurrent request"
//        };

//        private OtpApprovalService CreateApprovalService(AppDbContext ctx, string fixedCode)
//        {
//            var email = new Mock<IEmailService>();
//            email.Setup(e => e.BuildOperationalOtpEmail(
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
//                    It.IsAny<DateTime>(), It.IsAny<int>()))
//                .Returns("<html/>");
//            email.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
//                .Returns(Task.CompletedTask);

//            var env = new Mock<IWebHostEnvironment>();
//            env.Setup(e => e.EnvironmentName).Returns("Development");

//            return new OtpApprovalService(
//                new OtpChallengeRepository(ctx),
//                new WorkShiftRepository(ctx),
//                email.Object,
//                new FixedCodeGenerator(fixedCode, _codeGenerator),
//                _fingerprint,
//                NullLogger<OtpApprovalService>.Instance,
//                env.Object);
//        }

//        private WorkShiftService CreateWorkShiftService(AppDbContext ctx)
//        {
//            return new WorkShiftService(
//                new WorkShiftRepository(ctx),
//                Mock.Of<IPOSOrderRepository>(),
//                new OtpChallengeRepository(ctx),
//                _fingerprint,
//                NullLogger<WorkShiftService>.Instance);
//        }

//        private sealed class FixedCodeGenerator : IOtpCodeGenerator
//        {
//            private readonly string _code;
//            private readonly OtpCodeGenerator _inner;

//            public FixedCodeGenerator(string code, OtpCodeGenerator inner)
//            {
//                _code = code;
//                _inner = inner;
//            }

//            public string Generate() => _code;
//            public string? NormalizeAndValidate(string? raw) => _inner.NormalizeAndValidate(raw);
//        }
//    }
//}
