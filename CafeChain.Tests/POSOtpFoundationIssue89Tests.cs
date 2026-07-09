using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Accounts;
using CafeChain.Application.Services.POS;
using CafeChain.Infrastructure.Repositories.Admin.POS;
using CafeChain.Models.Customers;
using CafeChain.Models.Operations;
using CafeChain.Models.Permissions;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.Extensions.Logging;
using Moq;

namespace CafeChain.Tests
{
    /// <summary>
    /// Issue #89 — OTP Foundation Backend Tests.
    /// 
    /// Scope: OtpApprovalService + OtpChallengeRepository logic.
    /// Sử dụng SQLite In-Memory (IntegrationTestBase) cho repository tests,
    /// Mock IEmailService (không cần SMTP thật).
    /// </summary>
    public class POSOtpFoundationIssue89Tests : IntegrationTestBase
    {
        private readonly Mock<IEmailService> _mockEmail;
        private readonly Mock<ILogger<OtpApprovalService>> _mockLogger;

        public POSOtpFoundationIssue89Tests()
        {
            _mockEmail = new Mock<IEmailService>();
            _mockEmail
                .Setup(x => x.BuildOperationalOtpEmail(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTime>(), It.IsAny<int>()))
                .Returns("<html>OTP Email</html>");
            _mockEmail
                .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            _mockLogger = new Mock<ILogger<OtpApprovalService>>();
        }

        // ================================================================
        // HELPERS
        // ================================================================

        /// <summary>
        /// Seed minimum data: Store, Roles, Accounts, Staff (requester + approver).
        /// Returns (requesterId, approverId).
        /// </summary>
        private async Task<(int requesterId, int approverId)> SeedCoreDataAsync()
        {
            using var ctx = CreateDbContext();

            // Store
            ctx.Stores.Add(new Store { StoreId = 1000, Name = "Chi nhánh Test", Active = true, CreatedAt = DateTime.UtcNow });

            // Roles already seeded by RoleConfiguration.HasData() via EnsureCreated():
            // RoleId 8 = StoreManager, 9 = ShiftSupervisor, 10 = Cashier

            // Requester (Cashier)
            var requesterAccount = new Account
            {
                AccountId = 100,
                Email = "cashier1@test.com",
                PasswordHash = "hashed",
                Active = true,
                CreatedAt = DateTime.UtcNow
            };
            ctx.Accounts.Add(requesterAccount);

            var requester = new Staff
            {
                StaffId = 100,
                AccountId = 100,
                StoreId = 1000,
                FullName = "Nguyễn Văn A",
                Active = true,
                CreatedAt = DateTime.UtcNow
            };
            ctx.Staffs.Add(requester);

            ctx.AccountRoles.Add(new AccountRole { AccountId = 100, RoleId = 10 }); // Cashier

            // Approver (ShiftSupervisor)
            var approverAccount = new Account
            {
                AccountId = 200,
                Email = "supervisor1@test.com",
                PasswordHash = "hashed",
                Active = true,
                CreatedAt = DateTime.UtcNow
            };
            ctx.Accounts.Add(approverAccount);

            var approver = new Staff
            {
                StaffId = 200,
                AccountId = 200,
                StoreId = 1000,
                FullName = "Trần Thị B",
                Active = true,
                CreatedAt = DateTime.UtcNow
            };
            ctx.Staffs.Add(approver);

            ctx.AccountRoles.Add(new AccountRole { AccountId = 200, RoleId = 9 }); // ShiftSupervisor

            await ctx.SaveChangesAsync();
            return (100, 200);
            // StoreId = 1000
        }

        /// <summary>
        /// Seed extra cashier-only staff (should NOT be picked as approver).
        /// </summary>
        private async Task SeedCashierOnlyStaffAsync()
        {
            using var ctx = CreateDbContext();

            var account = new Account
            {
                AccountId = 300,
                Email = "cashier2@test.com",
                PasswordHash = "hashed",
                Active = true,
                CreatedAt = DateTime.UtcNow
            };
            ctx.Accounts.Add(account);

            ctx.Staffs.Add(new Staff
            {
                StaffId = 300,
                AccountId = 300,
                StoreId = 1000,
                FullName = "Cashier Only",
                Active = true,
                CreatedAt = DateTime.UtcNow
            });

            ctx.AccountRoles.Add(new AccountRole { AccountId = 300, RoleId = 10 }); // Cashier only

            await ctx.SaveChangesAsync();
        }

        private const int TestStoreId = 1000;

        private OtpApprovalService CreateService()
        {
            var ctx = CreateDbContext();
            var repo = new OtpChallengeRepository(ctx);
            return new OtpApprovalService(repo, _mockEmail.Object, _mockLogger.Object);
        }

        private OtpRequestDto CreateValidRequest()
        {
            return new OtpRequestDto
            {
                ActionType = OtpConstants.ActionTypes.CashDifference,
                TargetType = OtpConstants.TargetTypes.Shifts,
                TargetId = 1,
                WorkShiftId = 1,
                Reason = "Tiền mặt thực tế thiếu 120.000 so với hệ thống"
            };
        }

        /// <summary>
        /// Helper: Request OTP and return the challenge PublicId for further tests.
        /// </summary>
        private async Task<Guid> RequestAndGetPublicIdAsync(OtpApprovalService service, int requesterId)
        {
            var result = await service.RequestOtpAsync(CreateValidRequest(), requesterId, TestStoreId);
            Assert.True(result.IsSuccess, $"Request failed: {result.Message}");
            Assert.NotNull(result.Data?.OtpChallengePublicId);
            return result.Data!.OtpChallengePublicId!.Value;
        }

        /// <summary>
        /// Get the actual OTP code by extracting from the SendAsync call arg (the email body was built
        /// with the OTP code). Since we control the mock, we capture the OTP from the service internals
        /// by reading the OtpHash from DB and finding the code that verifies against it.
        /// 
        /// Since we can't reverse BCrypt, we intercept the BuildOperationalOtpEmail call instead.
        /// </summary>
        private string? _capturedOtpCode;

        private OtpApprovalService CreateServiceWithOtpCapture()
        {
            _capturedOtpCode = null;
            _mockEmail
                .Setup(x => x.BuildOperationalOtpEmail(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTime>(), It.IsAny<int>()))
                .Callback<string, string, string, string, string, string, DateTime, int>(
                    (otp, _, _, _, _, _, _, _) => _capturedOtpCode = otp)
                .Returns("<html>OTP Email</html>");

            var ctx = CreateDbContext();
            var repo = new OtpChallengeRepository(ctx);
            return new OtpApprovalService(repo, _mockEmail.Object, _mockLogger.Object);
        }

        // ================================================================
        // TEST 1: Request OTP tạo challenge Pending
        // ================================================================
        [Fact]
        public async Task Request_CreatesChallenge_WithPendingStatus()
        {
            await SeedCoreDataAsync();
            var service = CreateService();

            var result = await service.RequestOtpAsync(CreateValidRequest(), 100, TestStoreId);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(OtpConstants.Statuses.Pending, result.Data!.Status);
            Assert.True(result.Data.ExpiresInSeconds > 0);
            Assert.NotNull(result.Data.OtpChallengePublicId);
        }

        // ================================================================
        // TEST 2: OTP hash lưu, plain OTP không nằm trong DB
        // ================================================================
        [Fact]
        public async Task Request_StoresHashedOtp_NotPlainText()
        {
            await SeedCoreDataAsync();
            var service = CreateServiceWithOtpCapture();

            var result = await service.RequestOtpAsync(CreateValidRequest(), 100, TestStoreId);
            Assert.True(result.IsSuccess);

            // Verify OTP was captured
            Assert.NotNull(_capturedOtpCode);
            Assert.Equal(OtpConstants.CodeLength, _capturedOtpCode!.Length);
            Assert.True(_capturedOtpCode.All(char.IsDigit));

            // Verify DB stores hash, not plain OTP
            using var ctx = CreateDbContext();
            var challenge = ctx.OtpChallenges.First();
            Assert.NotEqual(_capturedOtpCode, challenge.OtpHash);
            Assert.True(BCrypt.Net.BCrypt.Verify(_capturedOtpCode, challenge.OtpHash));
        }

        // ================================================================
        // TEST 3: Valid OTP chuyển status Approved
        // ================================================================
        [Fact]
        public async Task Verify_ValidOtp_ChangesStatusToApproved()
        {
            await SeedCoreDataAsync();
            var service = CreateServiceWithOtpCapture();

            var publicId = await RequestAndGetPublicIdAsync(service, 100);

            var verifyResult = await service.VerifyOtpAsync(new OtpVerifyDto
            {
                OtpChallengePublicId = publicId,
                OtpCode = _capturedOtpCode!
            });

            Assert.True(verifyResult.IsSuccess, $"Verify failed: {verifyResult.Message}");
            Assert.Equal(OtpConstants.Statuses.Approved, verifyResult.Data!.Status);
        }

        // ================================================================
        // TEST 4: Verify lại OTP đã Approved bị reject
        // ================================================================
        [Fact]
        public async Task Verify_AlreadyApproved_IsRejected()
        {
            await SeedCoreDataAsync();
            var service = CreateServiceWithOtpCapture();

            var publicId = await RequestAndGetPublicIdAsync(service, 100);

            // First verify — success
            await service.VerifyOtpAsync(new OtpVerifyDto
            {
                OtpChallengePublicId = publicId,
                OtpCode = _capturedOtpCode!
            });

            // Second verify — should fail
            var secondResult = await service.VerifyOtpAsync(new OtpVerifyDto
            {
                OtpChallengePublicId = publicId,
                OtpCode = _capturedOtpCode!
            });

            Assert.False(secondResult.IsSuccess);
            Assert.Contains("xác nhận", secondResult.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ================================================================
        // TEST 5: Expired OTP bị reject
        // ================================================================
        [Fact]
        public async Task Verify_ExpiredOtp_IsRejected()
        {
            await SeedCoreDataAsync();
            var service = CreateServiceWithOtpCapture();

            var publicId = await RequestAndGetPublicIdAsync(service, 100);

            // Manually expire the challenge in DB
            using (var ctx = CreateDbContext())
            {
                var challenge = ctx.OtpChallenges.First(c => c.PublicId == publicId);
                challenge.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
                await ctx.SaveChangesAsync();
            }

            // Re-create service to get fresh DB context
            var service2 = CreateServiceWithOtpCapture();
            var result = await service2.VerifyOtpAsync(new OtpVerifyDto
            {
                OtpChallengePublicId = publicId,
                OtpCode = _capturedOtpCode!
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("hết hạn", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ================================================================
        // TEST 6: Sai OTP tăng FailedAttempts
        // ================================================================
        [Fact]
        public async Task Verify_WrongOtp_IncrementsFailedAttempts()
        {
            await SeedCoreDataAsync();
            var service = CreateServiceWithOtpCapture();

            var publicId = await RequestAndGetPublicIdAsync(service, 100);

            var result = await service.VerifyOtpAsync(new OtpVerifyDto
            {
                OtpChallengePublicId = publicId,
                OtpCode = "000000" // wrong code
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.MaxFailedAttempts - 1, result.Data!.RemainingAttempts);
        }

        // ================================================================
        // TEST 7: Sai 5 lần chuyển Locked
        // ================================================================
        [Fact]
        public async Task Verify_MaxFailedAttempts_LocksChallenge()
        {
            await SeedCoreDataAsync();
            var service = CreateServiceWithOtpCapture();

            var publicId = await RequestAndGetPublicIdAsync(service, 100);

            // Fail MaxFailedAttempts times
            for (int i = 0; i < OtpConstants.MaxFailedAttempts; i++)
            {
                // Need fresh service each time since the tracked entity is from different context
                var svc = CreateService();
                await svc.VerifyOtpAsync(new OtpVerifyDto
                {
                    OtpChallengePublicId = publicId,
                    OtpCode = "999999"
                });
            }

            // Now try with correct OTP — should be locked
            var lockedService = CreateServiceWithOtpCapture();
            var result = await lockedService.VerifyOtpAsync(new OtpVerifyDto
            {
                OtpChallengePublicId = publicId,
                OtpCode = _capturedOtpCode ?? "123456"
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.Statuses.Locked, result.Data!.Status);
        }

        // ================================================================
        // TEST 8: Resend chưa đủ cooldown bị reject
        // ================================================================
        [Fact]
        public async Task Resend_BeforeCooldown_IsRejected()
        {
            await SeedCoreDataAsync();
            var service = CreateServiceWithOtpCapture();

            var publicId = await RequestAndGetPublicIdAsync(service, 100);

            // Immediately resend — should fail (cooldown not elapsed)
            var result = await service.ResendOtpAsync(new OtpResendDto
            {
                OtpChallengePublicId = publicId
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("đợi", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ================================================================
        // TEST 9: Resend quá limit bị reject
        // ================================================================
        [Fact]
        public async Task Resend_ExceedsLimit_IsRejected()
        {
            await SeedCoreDataAsync();
            var service = CreateServiceWithOtpCapture();

            var publicId = await RequestAndGetPublicIdAsync(service, 100);

            // Set resend count to max in DB
            using (var ctx = CreateDbContext())
            {
                var challenge = ctx.OtpChallenges.First(c => c.PublicId == publicId);
                challenge.ResendCount = OtpConstants.MaxResendCount;
                challenge.LastSentAt = DateTime.UtcNow.AddMinutes(-5); // bypass cooldown
                await ctx.SaveChangesAsync();
            }

            var service2 = CreateService();
            var result = await service2.ResendOtpAsync(new OtpResendDto
            {
                OtpChallengePublicId = publicId
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("vượt quá", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ================================================================
        // TEST 10: Resend hợp lệ đổi hash, OTP cũ không verify được
        // ================================================================
        [Fact]
        public async Task Resend_Valid_InvalidatesOldOtp()
        {
            await SeedCoreDataAsync();
            var service = CreateServiceWithOtpCapture();

            var publicId = await RequestAndGetPublicIdAsync(service, 100);
            var oldOtp = _capturedOtpCode!;

            // Bypass cooldown
            using (var ctx = CreateDbContext())
            {
                var challenge = ctx.OtpChallenges.First(c => c.PublicId == publicId);
                challenge.LastSentAt = DateTime.UtcNow.AddMinutes(-5);
                await ctx.SaveChangesAsync();
            }

            // Resend
            var resendService = CreateServiceWithOtpCapture();
            var resendResult = await resendService.ResendOtpAsync(new OtpResendDto
            {
                OtpChallengePublicId = publicId
            });

            Assert.True(resendResult.IsSuccess, $"Resend failed: {resendResult.Message}");
            var newOtp = _capturedOtpCode!;

            // Old OTP should no longer verify
            var verifyOldService = CreateService();
            var verifyOld = await verifyOldService.VerifyOtpAsync(new OtpVerifyDto
            {
                OtpChallengePublicId = publicId,
                OtpCode = oldOtp
            });

            // Old OTP might coincidentally match the new hash (extremely unlikely with 6 digits),
            // but the hash changed, so if old != new, it should fail
            if (oldOtp != newOtp)
            {
                Assert.False(verifyOld.IsSuccess);
            }

            // New OTP should verify
            var verifyNewService = CreateService();
            var verifyNew = await verifyNewService.VerifyOtpAsync(new OtpVerifyDto
            {
                OtpChallengePublicId = publicId,
                OtpCode = newOtp
            });

            Assert.True(verifyNew.IsSuccess, $"New OTP verify failed: {verifyNew.Message}");
        }

        // ================================================================
        // TEST 11: Cashier không được chọn làm approver
        // ================================================================
        [Fact]
        public async Task GetOtpApprover_ExcludesCashierOnly()
        {
            await SeedCoreDataAsync();
            await SeedCashierOnlyStaffAsync();

            using var ctx = CreateDbContext();
            var repo = new OtpChallengeRepository(ctx);

            var approver = await repo.GetOtpApproverAsync(TestStoreId, DateTime.UtcNow);

            Assert.NotNull(approver);
            // Approver should be StaffId 200 (ShiftSupervisor), NOT 300 (Cashier)
            Assert.Equal(200, approver!.StaffId);
        }

        // ================================================================
        // TEST 12: Chỉ Ca trưởng/Cửa hàng trưởng active cùng store có email
        // ================================================================
        [Fact]
        public async Task GetOtpApprover_OnlyActiveSupervisorWithEmailInSameStore()
        {
            // Seed only — no approver at store 9999
            using (var ctx = CreateDbContext())
            {
                ctx.Stores.Add(new Store { StoreId = 9999, Name = "Empty Store", Active = true, CreatedAt = DateTime.UtcNow });
                await ctx.SaveChangesAsync();
            }

            using var ctx2 = CreateDbContext();
            var repo = new OtpChallengeRepository(ctx2);

            var approver = await repo.GetOtpApproverAsync(9999, DateTime.UtcNow);

            Assert.Null(approver); // No supervisor at store 9999

            // Now verify store 1000 works after seeding
            await SeedCoreDataAsync();
            using var ctx3 = CreateDbContext();
            var repo2 = new OtpChallengeRepository(ctx3);

            var approver2 = await repo2.GetOtpApproverAsync(TestStoreId, DateTime.UtcNow);
            Assert.NotNull(approver2);
            Assert.Equal(200, approver2!.StaffId); // ShiftSupervisor at store 1000
            Assert.NotNull(approver2.Account);
            Assert.False(string.IsNullOrWhiteSpace(approver2.Account.Email));
        }

        // ================================================================
        // TEST 13 (bonus): Email service is mocked, not real SMTP
        // ================================================================
        [Fact]
        public async Task Request_UsesEmailServiceMock_NotRealSmtp()
        {
            await SeedCoreDataAsync();
            var service = CreateService();

            var result = await service.RequestOtpAsync(CreateValidRequest(), 100, TestStoreId);

            Assert.True(result.IsSuccess);
            _mockEmail.Verify(
                x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Once);
        }
    }
}
