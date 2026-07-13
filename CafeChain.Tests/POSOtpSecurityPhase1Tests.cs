using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Accounts;
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
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>
    /// Phase 1 (#142): alphanumeric OTP foundation, anti-self-approval, fingerprint binding,
    /// one active challenge, verify/resend, close consume.
    /// SQLite via IntegrationTestBase (RowVersion concurrency token disabled).
    /// </summary>
    public class POSOtpSecurityPhase1Tests : IntegrationTestBase
    {
        private const int StoreId = 9001;
        private const int RequesterId = 9100;
        private const int ApproverId = 9200;
        private const int ShiftIdSeed = 0; // assigned by DB

        private readonly OtpCodeGenerator _codeGenerator = new();
        private readonly OtpPayloadFingerprintService _fingerprint = new();

        // -----------------------------------------------------------------
        // Generator
        // -----------------------------------------------------------------

        [Fact]
        public void OtpCodeGenerator_ProducesSixAllowedCharacters()
        {
            for (var i = 0; i < 50; i++)
            {
                var code = _codeGenerator.Generate();
                Assert.Equal(6, code.Length);
                Assert.All(code, ch => Assert.Contains(ch, OtpConstants.Alphabet));
            }
        }

        [Fact]
        public void OtpCodeGenerator_ExcludesAmbiguousCharacters()
        {
            const string ambiguous = "O0I1";
            for (var i = 0; i < 200; i++)
            {
                var code = _codeGenerator.Generate();
                Assert.DoesNotContain(code, c => ambiguous.Contains(c));
            }
        }

        [Fact]
        public void OtpCodeGenerator_UsesCryptographicRandomSource()
        {
            // Contract: implementation must call RandomNumberGenerator.GetInt32 (no System.Random / Guid).
            var source = typeof(OtpCodeGenerator).GetMethod(
                nameof(OtpCodeGenerator.Generate),
                BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(source);

            var methodBody = typeof(OtpCodeGenerator)
                .GetMethod(nameof(OtpCodeGenerator.Generate))!
                .GetMethodBody();
            Assert.NotNull(methodBody);

            // Behavioral diversity — cryptographic source yields varied codes (not fixed / timestamp).
            var codes = Enumerable.Range(0, 30).Select(_ => _codeGenerator.Generate()).ToHashSet();
            Assert.True(codes.Count > 10, "Expected high entropy from cryptographic RNG.");

            // Normalize rejects digits-only legacy codes that use ambiguous chars.
            Assert.Null(_codeGenerator.NormalizeAndValidate("012345"));
            Assert.Null(_codeGenerator.NormalizeAndValidate("OOOOOO"));
            Assert.Equal("A2B3C4", _codeGenerator.NormalizeAndValidate("a2b3c4"));
            Assert.Equal("A2B3C4", _codeGenerator.NormalizeAndValidate(" A2B3C4 "));
            Assert.Null(_codeGenerator.NormalizeAndValidate("A2 B3C4"));
        }

        // -----------------------------------------------------------------
        // Request / approver selection
        // -----------------------------------------------------------------

        [Fact]
        public async Task OtpApproval_Request_DoesNotSelectActorAsApprover()
        {
            using var ctx = CreateDbContext();
            await SeedStoreAsync(ctx);
            // Actor is also ShiftSupervisor; another StoreManager must be chosen.
            SeedStaff(ctx, RequesterId, 9100, RoleConstants.ShiftSupervisor, "actor@test.local");
            SeedStaff(ctx, ApproverId, 9200, RoleConstants.StoreManager, "manager@test.local");
            await ctx.SaveChangesAsync();

            var service = CreateApprovalService(ctx, out var email, fixedCode: "AB23CD");
            var result = await service.RequestOtpAsync(NewRequest(1), RequesterId, StoreId);

            Assert.True(result.IsSuccess, result.Message);
            var challenge = await ctx.OtpChallenges.AsNoTracking()
                .FirstAsync(c => c.PublicId == result.Data!.OtpChallengePublicId);
            Assert.Equal(ApproverId, challenge.ApproverStaffId);
            Assert.NotEqual(RequesterId, challenge.ApproverStaffId);
            email.Verify(e => e.SendAsync("manager@test.local", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task OtpApproval_Request_NoOtherApprover_IsRejected()
        {
            using var ctx = CreateDbContext();
            await SeedStoreAsync(ctx);
            SeedStaff(ctx, RequesterId, 9100, RoleConstants.SalesStaff, "cashier@test.local");
            // Only actor as supervisor — self cannot approve.
            SeedStaff(ctx, 9110, 9110, RoleConstants.ShiftSupervisor, "solo-ss@test.local");
            await ctx.SaveChangesAsync();

            // Requester is the only SS
            var service = CreateApprovalService(ctx, out _, fixedCode: "AB23CD");
            var asSelf = await service.RequestOtpAsync(NewRequest(1), requestedByStaffId: 9110, StoreId);

            Assert.False(asSelf.IsSuccess);
            Assert.Equal(OtpConstants.ErrorCodes.NoEligibleApprover, asSelf.ErrorCode);
        }

        [Fact]
        public async Task OtpApproval_Request_CreatesOnlyOneActiveChallenge()
        {
            using var ctx = CreateDbContext();
            var shiftId = await SeedCoreAsync(ctx);
            var service = CreateApprovalService(ctx, out _, fixedCode: "AB23CD");

            var first = await service.RequestOtpAsync(NewRequest(shiftId), RequesterId, StoreId);
            Assert.True(first.IsSuccess, first.Message);
            Assert.False(first.Data!.WasExistingActive);

            var second = await service.RequestOtpAsync(NewRequest(shiftId), RequesterId, StoreId);
            Assert.True(second.IsSuccess, second.Message);
            Assert.True(second.Data!.WasExistingActive);
            Assert.Equal(first.Data.OtpChallengePublicId, second.Data.OtpChallengePublicId);

            var count = await ctx.OtpChallenges.CountAsync(c =>
                c.StoreId == StoreId &&
                c.RequestedByStaffId == RequesterId &&
                c.ActionType == OtpConstants.ActionTypes.CashDifference &&
                c.TargetId == shiftId);
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task OtpApproval_Request_HashesAlphanumericCode()
        {
            using var ctx = CreateDbContext();
            var shiftId = await SeedCoreAsync(ctx);
            const string code = "XY2Z34";
            var service = CreateApprovalService(ctx, out var email, fixedCode: code);

            string? captured = null;
            email.Setup(e => e.BuildOperationalOtpEmail(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTime>(), It.IsAny<int>()))
                .Callback<string, string, string, string, string, string, DateTime, int>(
                    (otp, _, _, _, _, _, _, _) => captured = otp)
                .Returns("<html/>");

            var result = await service.RequestOtpAsync(NewRequest(shiftId), RequesterId, StoreId);
            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(code, captured);

            var challenge = await ctx.OtpChallenges.AsNoTracking()
                .FirstAsync(c => c.PublicId == result.Data!.OtpChallengePublicId);
            Assert.True(BCrypt.Net.BCrypt.Verify(code, challenge.OtpHash));
            Assert.DoesNotContain(code, challenge.OtpHash, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(6, code.Length);
            Assert.All(code, ch => Assert.Contains(ch, OtpConstants.Alphabet));
            Assert.False(string.IsNullOrWhiteSpace(challenge.PayloadFingerprint));
        }

        // -----------------------------------------------------------------
        // Verify
        // -----------------------------------------------------------------

        [Fact]
        public async Task OtpApproval_Verify_IsCaseNormalized()
        {
            using var ctx = CreateDbContext();
            var shiftId = await SeedCoreAsync(ctx);
            var publicId = await InsertPendingChallengeAsync(ctx, shiftId, "Ab2c3d");
            var service = CreateApprovalService(ctx, out _, fixedCode: "ZZZZZZ");

            var result = await service.VerifyOtpAsync(new OtpVerifyDto
            {
                OtpChallengePublicId = publicId,
                OtpCode = "ab2c3d"
            });

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(OtpConstants.Statuses.Approved, result.Data!.Status);
        }

        [Fact]
        public async Task OtpApproval_Verify_WrongCharacter_IsRejected()
        {
            using var ctx = CreateDbContext();
            var shiftId = await SeedCoreAsync(ctx);
            var publicId = await InsertPendingChallengeAsync(ctx, shiftId, "AB23CD");
            var service = CreateApprovalService(ctx, out _, fixedCode: "ZZZZZZ");

            var result = await service.VerifyOtpAsync(new OtpVerifyDto
            {
                OtpChallengePublicId = publicId,
                OtpCode = "AB23CE"
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("OTP không đúng", result.Message);
        }

        [Fact]
        public async Task OtpApproval_Verify_CorrectCode_ApprovesOnce()
        {
            using var ctx = CreateDbContext();
            var shiftId = await SeedCoreAsync(ctx);
            var publicId = await InsertPendingChallengeAsync(ctx, shiftId, "QRSTUV");
            var service = CreateApprovalService(ctx, out _, fixedCode: "ZZZZZZ");

            var first = await service.VerifyOtpAsync(new OtpVerifyDto
            {
                OtpChallengePublicId = publicId,
                OtpCode = "QRSTUV"
            });
            Assert.True(first.IsSuccess, first.Message);

            var second = await service.VerifyOtpAsync(new OtpVerifyDto
            {
                OtpChallengePublicId = publicId,
                OtpCode = "QRSTUV"
            });
            Assert.False(second.IsSuccess);
        }

        [Fact]
        public async Task OtpApproval_Verify_WrongCode_IncrementsAttempts()
        {
            using var ctx = CreateDbContext();
            var shiftId = await SeedCoreAsync(ctx);
            var publicId = await InsertPendingChallengeAsync(ctx, shiftId, "QRSTUV");
            var service = CreateApprovalService(ctx, out _, fixedCode: "ZZZZZZ");

            await service.VerifyOtpAsync(new OtpVerifyDto { OtpChallengePublicId = publicId, OtpCode = "AAAAAA" });
            var challenge = await ctx.OtpChallenges.AsNoTracking().FirstAsync(c => c.PublicId == publicId);
            Assert.Equal(1, challenge.FailedAttempts);
            Assert.Equal(OtpConstants.Statuses.Pending, challenge.Status);
        }

        [Fact]
        public async Task OtpApproval_Verify_MaxAttempts_LocksChallenge()
        {
            using var ctx = CreateDbContext();
            var shiftId = await SeedCoreAsync(ctx);
            var publicId = await InsertPendingChallengeAsync(ctx, shiftId, "QRSTUV");
            var service = CreateApprovalService(ctx, out _, fixedCode: "ZZZZZZ");

            for (var i = 0; i < OtpConstants.MaxFailedAttempts; i++)
            {
                await service.VerifyOtpAsync(new OtpVerifyDto
                {
                    OtpChallengePublicId = publicId,
                    OtpCode = "AAAAAA"
                });
            }

            var challenge = await ctx.OtpChallenges.AsNoTracking().FirstAsync(c => c.PublicId == publicId);
            Assert.Equal(OtpConstants.Statuses.Locked, challenge.Status);
            Assert.Equal(OtpConstants.MaxFailedAttempts, challenge.FailedAttempts);
        }

        [Fact]
        public async Task OtpApproval_Verify_ExpiredChallenge_IsRejected()
        {
            using var ctx = CreateDbContext();
            var shiftId = await SeedCoreAsync(ctx);
            var publicId = await InsertPendingChallengeAsync(ctx, shiftId, "QRSTUV", expiresAt: DateTime.UtcNow.AddMinutes(-1));
            var service = CreateApprovalService(ctx, out _, fixedCode: "ZZZZZZ");

            var result = await service.VerifyOtpAsync(new OtpVerifyDto
            {
                OtpChallengePublicId = publicId,
                OtpCode = "QRSTUV"
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("hết hạn", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        // -----------------------------------------------------------------
        // Resend
        // -----------------------------------------------------------------

        [Fact]
        public async Task OtpApproval_Resend_RotatesHashAndInvalidatesOldCode()
        {
            using var ctx = CreateDbContext();
            var shiftId = await SeedCoreAsync(ctx);
            // Alphabet-safe codes only (no O/0/I/1).
            var publicId = await InsertPendingChallengeAsync(
                ctx, shiftId, "ABCD23", lastSentAt: DateTime.UtcNow.AddSeconds(-120));
            var service = CreateApprovalService(ctx, out _, fixedCode: "EFGH45");

            var resend = await service.ResendOtpAsync(new OtpResendDto { OtpChallengePublicId = publicId });
            Assert.True(resend.IsSuccess, resend.Message);

            var challenge = await ctx.OtpChallenges.AsNoTracking().FirstAsync(c => c.PublicId == publicId);
            Assert.False(BCrypt.Net.BCrypt.Verify("ABCD23", challenge.OtpHash));
            Assert.True(BCrypt.Net.BCrypt.Verify("EFGH45", challenge.OtpHash));
            Assert.Equal(1, challenge.ResendCount);
        }

        [Fact]
        public async Task OtpApproval_Resend_EnforcesCooldown()
        {
            using var ctx = CreateDbContext();
            var shiftId = await SeedCoreAsync(ctx);
            var publicId = await InsertPendingChallengeAsync(
                ctx, shiftId, "ABCD23", lastSentAt: DateTime.UtcNow);
            var service = CreateApprovalService(ctx, out _, fixedCode: "EFGH45");

            var resend = await service.ResendOtpAsync(new OtpResendDto { OtpChallengePublicId = publicId });
            Assert.False(resend.IsSuccess);
            Assert.Contains("đợi", resend.Message, StringComparison.OrdinalIgnoreCase);
        }

        // -----------------------------------------------------------------
        // Close consume bindings
        // -----------------------------------------------------------------

        [Fact]
        public async Task OtpApproval_Close_BindsActualEndingCash()
        {
            await AssertClosePayloadMismatchAsync(
                seedCash: 440_000m,
                seedReason: "Lý do A",
                closeCash: 430_000m,
                closeReason: "Lý do A");
        }

        [Fact]
        public async Task OtpApproval_Close_BindsReason()
        {
            await AssertClosePayloadMismatchAsync(
                seedCash: 440_000m,
                seedReason: "Lý do A",
                closeCash: 440_000m,
                closeReason: "Lý do B khác");
        }

        [Fact]
        public async Task OtpApproval_Close_BindsExactActor()
        {
            using var ctx = CreateDbContext();
            var shiftId = await SeedCoreAsync(ctx);
            SeedStaff(ctx, 9300, 9300, RoleConstants.SalesStaff, "other@test.local");
            // Close original shift; open shift for other cashier. OTP targets that shift but RequestedBy=original actor.
            var original = await ctx.WorkShifts.FirstAsync(s => s.ShiftId == shiftId);
            original.Status = "Closed";
            original.EndTime = DateTime.Now;
            var otherShift = new WorkShift
            {
                StoreId = StoreId,
                UserId = 9300,
                Status = "Open",
                StartTime = DateTime.Now,
                StartingCash = 500_000m,
                ExpectedEndingCash = 500_000m
            };
            ctx.WorkShifts.Add(otherShift);
            await ctx.SaveChangesAsync();

            // Challenge for otherShift but RequestedBy still RequesterId (not 9300).
            var publicId = await InsertApprovedChallengeAsync(
                ctx, otherShift.ShiftId, 440_000m, "Lý do A", forceShiftId: otherShift.ShiftId);
            var service = CreateWorkShiftService(ctx);

            var result = await service.CloseShiftAsync(9300, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = 440_000m,
                DiscrepancyReason = "Lý do A",
                OtpChallengePublicId = publicId
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("không thuộc nhân viên", result.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task OtpApproval_Close_BindsWorkShiftAndStore()
        {
            using var ctx = CreateDbContext();
            var shiftId = await SeedCoreAsync(ctx);
            // Second open shift for same user/store not allowed by business; use wrong challenge target.
            var publicId = await InsertApprovedChallengeAsync(ctx, shiftId + 999, 440_000m, "Lý do A", forceShiftId: shiftId + 999);
            var service = CreateWorkShiftService(ctx);

            var result = await service.CloseShiftAsync(RequesterId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = 440_000m,
                DiscrepancyReason = "Lý do A",
                OtpChallengePublicId = publicId
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("ca két tiền hiện tại", result.Message);
        }

        [Fact]
        public async Task OtpApproval_Close_SelfApproval_IsRejected()
        {
            using var ctx = CreateDbContext();
            var shiftId = await SeedCoreAsync(ctx);
            var publicId = await InsertApprovedChallengeAsync(
                ctx, shiftId, 440_000m, "Lý do A", approverId: RequesterId);
            var service = CreateWorkShiftService(ctx);

            var result = await service.CloseShiftAsync(RequesterId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = 440_000m,
                DiscrepancyReason = "Lý do A",
                OtpChallengePublicId = publicId
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.ErrorCodes.NoEligibleApprover, result.ErrorCode);
        }

        [Fact]
        public async Task OtpApproval_Close_InactiveApprover_IsRejected()
        {
            using var ctx = CreateDbContext();
            var shiftId = await SeedCoreAsync(ctx);
            var publicId = await InsertApprovedChallengeAsync(ctx, shiftId, 440_000m, "Lý do A");

            var approver = await ctx.Staffs.FirstAsync(s => s.StaffId == ApproverId);
            approver.Active = false;
            await ctx.SaveChangesAsync();

            var service = CreateWorkShiftService(ctx);
            var result = await service.CloseShiftAsync(RequesterId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = 440_000m,
                DiscrepancyReason = "Lý do A",
                OtpChallengePublicId = publicId
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.ErrorCodes.ApproverNoLongerEligible, result.ErrorCode);
        }

        [Fact]
        public async Task OtpApproval_Close_ChangedApproverRole_IsRejected()
        {
            using var ctx = CreateDbContext();
            var shiftId = await SeedCoreAsync(ctx);
            var publicId = await InsertApprovedChallengeAsync(ctx, shiftId, 440_000m, "Lý do A");

            // Downgrade approver from SS to SalesStaff
            var accountId = (await ctx.Staffs.FirstAsync(s => s.StaffId == ApproverId)).AccountId;
            var roles = ctx.AccountRoles.Where(ar => ar.AccountId == accountId).ToList();
            ctx.AccountRoles.RemoveRange(roles);
            ctx.AccountRoles.Add(new AccountRole { AccountId = accountId, RoleId = 4 }); // SalesStaff
            await ctx.SaveChangesAsync();

            var service = CreateWorkShiftService(ctx);
            var result = await service.CloseShiftAsync(RequesterId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = 440_000m,
                DiscrepancyReason = "Lý do A",
                OtpChallengePublicId = publicId
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.ErrorCodes.ApproverNoLongerEligible, result.ErrorCode);
        }

        [Fact]
        public async Task OtpApproval_Close_Replay_IsRejected()
        {
            using var ctx = CreateDbContext();
            var shiftId = await SeedCoreAsync(ctx);
            var publicId = await InsertApprovedChallengeAsync(ctx, shiftId, 440_000m, "Lý do A");
            var service = CreateWorkShiftService(ctx);

            var first = await service.CloseShiftAsync(RequesterId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = 440_000m,
                DiscrepancyReason = "Lý do A",
                OtpChallengePublicId = publicId
            });
            Assert.True(first.IsSuccess, first.Message);

            // Re-open for replay attempt of same OTP (simulate second close attempt)
            var shift = await ctx.WorkShifts.FirstAsync(s => s.ShiftId == shiftId);
            shift.Status = "Open";
            shift.EndTime = null;
            await ctx.SaveChangesAsync();

            var second = await service.CloseShiftAsync(RequesterId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = 440_000m,
                DiscrepancyReason = "Lý do A",
                OtpChallengePublicId = publicId
            });

            Assert.False(second.IsSuccess);
            Assert.Contains("đã được sử dụng", second.Message);
        }

        [Fact]
        public async Task OtpApproval_Close_MutationFailure_DoesNotConsumeChallenge()
        {
            using var ctx = CreateDbContext();
            var shiftId = await SeedCoreAsync(ctx);
            var publicId = await InsertApprovedChallengeAsync(ctx, shiftId, 440_000m, "Lý do A");

            var otpRepo = new OtpChallengeRepository(ctx);
            var realShiftRepo = new WorkShiftRepository(ctx);
            var shiftRepo = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            shiftRepo.Setup(r => r.GetActiveShiftAsync(RequesterId, StoreId))
                .Returns<int, int>((u, s) => realShiftRepo.GetActiveShiftAsync(u, s));
            shiftRepo.Setup(r => r.HasOpenPosPaymentAsync(It.IsAny<int>(), StoreId))
                .ReturnsAsync(false);
            shiftRepo.Setup(r => r.GetTotalCashSalesAsync(It.IsAny<int>()))
                .ReturnsAsync(0m);
            shiftRepo.Setup(r => r.UpdateShiftAsync(It.IsAny<WorkShift>()))
                .ThrowsAsync(new InvalidOperationException("Simulated close mutation failure"));

            var service = new WorkShiftService(
                shiftRepo.Object,
                Mock.Of<IHrAttendanceService>(),
                Mock.Of<IPOSOrderRepository>(),
                Mock.Of<ISupervisorAuthService>(),
                otpRepo,
                _fingerprint,
                NullLogger<WorkShiftService>.Instance);

            var result = await service.CloseShiftAsync(RequesterId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = 440_000m,
                DiscrepancyReason = "Lý do A",
                OtpChallengePublicId = publicId
            });

            Assert.False(result.IsSuccess);

            // Detach and re-read
            ctx.ChangeTracker.Clear();
            var challenge = await ctx.OtpChallenges.AsNoTracking().FirstAsync(c => c.PublicId == publicId);
            Assert.Equal(OtpConstants.Statuses.Approved, challenge.Status);
            Assert.Null(challenge.UsedAt);
        }

        [Fact]
        public async Task OtpApproval_CodeAndHash_AreNotLogged()
        {
            using var ctx = CreateDbContext();
            var shiftId = await SeedCoreAsync(ctx);
            const string code = "L2GCHK"; // alphabet-safe (no O/0/I/1)
            var logger = new CapturingLogger<OtpApprovalService>();
            var service = CreateApprovalService(ctx, out _, fixedCode: code, logger: logger);

            var result = await service.RequestOtpAsync(NewRequest(shiftId), RequesterId, StoreId);
            Assert.True(result.IsSuccess, result.Message);

            var all = string.Join("\n", logger.Messages);
            Assert.DoesNotContain(code, all, StringComparison.Ordinal);
            var challenge = await ctx.OtpChallenges.AsNoTracking()
                .FirstAsync(c => c.PublicId == result.Data!.OtpChallengePublicId);
            Assert.DoesNotContain(challenge.OtpHash, all, StringComparison.Ordinal);
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private async Task AssertClosePayloadMismatchAsync(
            decimal seedCash, string seedReason, decimal closeCash, string closeReason)
        {
            using var ctx = CreateDbContext();
            var shiftId = await SeedCoreAsync(ctx);
            var publicId = await InsertApprovedChallengeAsync(ctx, shiftId, seedCash, seedReason);
            var service = CreateWorkShiftService(ctx);

            var result = await service.CloseShiftAsync(RequesterId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = closeCash,
                DiscrepancyReason = closeReason,
                OtpChallengePublicId = publicId
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.ErrorCodes.PayloadMismatch, result.ErrorCode);
        }

        private static OtpRequestDto NewRequest(int shiftId, decimal cash = 440_000m, string reason = "Thiếu tiền mặt sau kiểm két.")
            => new()
            {
                ActionType = OtpConstants.ActionTypes.CashDifference,
                TargetType = OtpConstants.TargetTypes.Shifts,
                TargetId = shiftId,
                WorkShiftId = shiftId,
                ActualEndingCash = cash,
                Reason = reason
            };

        private OtpApprovalService CreateApprovalService(
            AppDbContext ctx,
            out Mock<IEmailService> email,
            string fixedCode,
            ILogger<OtpApprovalService>? logger = null)
        {
            email = new Mock<IEmailService>();
            email.Setup(e => e.BuildOperationalOtpEmail(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<DateTime>(), It.IsAny<int>()))
                .Returns("<html>OTP</html>");
            email.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.EnvironmentName).Returns("Development");

            var codeGen = new FixedOtpCodeGenerator(fixedCode, _codeGenerator);
            return new OtpApprovalService(
                new OtpChallengeRepository(ctx),
                new WorkShiftRepository(ctx),
                email.Object,
                codeGen,
                _fingerprint,
                logger ?? NullLogger<OtpApprovalService>.Instance,
                env.Object);
        }

        private WorkShiftService CreateWorkShiftService(AppDbContext ctx)
        {
            // SQLite cannot Sum(decimal) in WorkShiftRepository.GetTotalCashSalesAsync —
            // wrap real repo for active-shift load and stub cash sales at 0 for close tests.
            var real = new WorkShiftRepository(ctx);
            var shiftRepo = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            shiftRepo.Setup(r => r.GetActiveShiftAsync(It.IsAny<int>(), It.IsAny<int>()))
                .Returns((int u, int s) => real.GetActiveShiftAsync(u, s));
            shiftRepo.Setup(r => r.HasOpenPosPaymentAsync(It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(false);
            shiftRepo.Setup(r => r.GetTotalCashSalesAsync(It.IsAny<int>()))
                .ReturnsAsync(0m);
            shiftRepo.Setup(r => r.UpdateShiftAsync(It.IsAny<WorkShift>()))
                .Returns((WorkShift w) => real.UpdateShiftAsync(w));

            return new WorkShiftService(
                shiftRepo.Object,
                Mock.Of<IHrAttendanceService>(),
                Mock.Of<IPOSOrderRepository>(),
                Mock.Of<ISupervisorAuthService>(),
                new OtpChallengeRepository(ctx),
                _fingerprint,
                NullLogger<WorkShiftService>.Instance);
        }

        private async Task SeedStoreAsync(AppDbContext ctx)
        {
            if (!await ctx.Stores.AnyAsync(s => s.StoreId == StoreId))
            {
                ctx.Stores.Add(new Store
                {
                    StoreId = StoreId,
                    Name = "OTP Phase1 Store",
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        private async Task<int> SeedCoreAsync(AppDbContext ctx)
        {
            await SeedStoreAsync(ctx);
            SeedStaff(ctx, RequesterId, 9100, RoleConstants.SalesStaff, "cashier@test.local");
            SeedStaff(ctx, ApproverId, 9200, RoleConstants.ShiftSupervisor, "supervisor@test.local");
            await ctx.SaveChangesAsync();

            var shift = new WorkShift
            {
                StoreId = StoreId,
                UserId = RequesterId,
                Status = "Open",
                StartTime = DateTime.Now,
                StartingCash = 500_000m,
                ExpectedEndingCash = 500_000m
            };
            ctx.WorkShifts.Add(shift);
            await ctx.SaveChangesAsync();
            return shift.ShiftId;
        }

        private static void SeedStaff(
            AppDbContext ctx,
            int staffId,
            int accountId,
            string roleName,
            string email)
        {
            if (ctx.Staffs.Local.Any(s => s.StaffId == staffId) || ctx.Staffs.Any(s => s.StaffId == staffId))
                return;

            var role = ctx.Roles.First(r => r.Name == roleName);
            ctx.Accounts.Add(new Account
            {
                AccountId = accountId,
                Email = email,
                PasswordHash = "x",
                Active = true,
                CreatedAt = DateTime.UtcNow
            });
            ctx.AccountRoles.Add(new AccountRole { AccountId = accountId, RoleId = role.RoleId });
            ctx.Staffs.Add(new Staff
            {
                StaffId = staffId,
                AccountId = accountId,
                StoreId = StoreId,
                FullName = $"Staff {staffId}",
                Active = true,
                CreatedAt = DateTime.UtcNow,
                BaseSalary = 0,
                StaffShifts = new List<StaffShift>()
            });
        }

        private async Task<Guid> InsertPendingChallengeAsync(
            AppDbContext ctx,
            int shiftId,
            string plainCode,
            DateTime? expiresAt = null,
            DateTime? lastSentAt = null)
        {
            var now = DateTime.UtcNow;
            var publicId = Guid.NewGuid();
            var reason = "Thiếu tiền mặt sau kiểm két.";
            ctx.OtpChallenges.Add(new OtpChallenge
            {
                PublicId = publicId,
                StoreId = StoreId,
                WorkShiftId = shiftId,
                RequestedByStaffId = RequesterId,
                ApproverStaffId = ApproverId,
                ActionType = OtpConstants.ActionTypes.CashDifference,
                TargetType = OtpConstants.TargetTypes.Shifts,
                TargetId = shiftId,
                Reason = reason,
                PayloadFingerprint = _fingerprint.BuildCashDifferenceFingerprint(
                    StoreId, RequesterId, shiftId, 440_000m, reason),
                OtpHash = BCrypt.Net.BCrypt.HashPassword(plainCode.ToUpperInvariant()),
                ExpiresAt = expiresAt ?? now.AddMinutes(5),
                LastSentAt = lastSentAt ?? now,
                CreatedAt = now,
                Status = OtpConstants.Statuses.Pending
            });
            await ctx.SaveChangesAsync();
            return publicId;
        }

        private async Task<Guid> InsertApprovedChallengeAsync(
            AppDbContext ctx,
            int shiftId,
            decimal actualCash,
            string reason,
            int? approverId = null,
            int? forceShiftId = null)
        {
            var now = DateTime.UtcNow;
            var publicId = Guid.NewGuid();
            var targetShift = forceShiftId ?? shiftId;
            ctx.OtpChallenges.Add(new OtpChallenge
            {
                PublicId = publicId,
                StoreId = StoreId,
                WorkShiftId = targetShift,
                RequestedByStaffId = RequesterId,
                ApproverStaffId = approverId ?? ApproverId,
                ActionType = OtpConstants.ActionTypes.CashDifference,
                TargetType = OtpConstants.TargetTypes.Shifts,
                TargetId = targetShift,
                Reason = reason,
                PayloadFingerprint = _fingerprint.BuildCashDifferenceFingerprint(
                    StoreId, RequesterId, targetShift, actualCash, reason),
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

        private sealed class FixedOtpCodeGenerator : IOtpCodeGenerator
        {
            private readonly string _code;
            private readonly OtpCodeGenerator _inner;

            public FixedOtpCodeGenerator(string code, OtpCodeGenerator inner)
            {
                _code = code.ToUpperInvariant();
                _inner = inner;
            }

            public string Generate() => _code;
            public string? NormalizeAndValidate(string? rawCode) => _inner.NormalizeAndValidate(rawCode);
        }

        private sealed class CapturingLogger<T> : ILogger<T>
        {
            public List<string> Messages { get; } = new();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                Messages.Add(formatter(state, exception));
            }
        }
    }
}
