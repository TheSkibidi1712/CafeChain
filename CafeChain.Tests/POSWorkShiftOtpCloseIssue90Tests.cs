using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Attendance;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Application.Services.POS;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Operations;
using CafeChain.Models.Stores;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>
    /// Issue #90 / Phase 1: WorkShift Cash Discrepancy OTP Backend Integration.
    /// CloseShiftAsync requires OTP when discrepancy exceeds threshold
    /// and consumes OTP atomically on successful close (fingerprint + anti-self).
    /// </summary>
    public class POSWorkShiftOtpCloseIssue90Tests
    {
        private const int UserId = 17;
        private const int StoreId = 3;
        private const int ShiftId = 90;
        private const int ApproverStaffId = 200;
        private const decimal StartingCash = 500_000m;
        private const decimal HighDiscrepancyActual = 440_000m;
        private const string DefaultReason = "Thiếu tiền mặt sau kiểm két.";

        private static readonly OtpPayloadFingerprintService Fingerprint = new();

        private static readonly Guid ValidOtpPublicId =
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        private static WorkShift CreateOpenShift(decimal expectedEndingCash = StartingCash)
        {
            return new WorkShift
            {
                ShiftId = ShiftId,
                StoreId = StoreId,
                UserId = UserId,
                Status = "Open",
                StartTime = DateTime.Now,
                StartingCash = StartingCash,
                ExpectedEndingCash = expectedEndingCash
            };
        }

        private static OtpChallenge CreateApprovedChallenge(
            int shiftId = ShiftId,
            int storeId = StoreId,
            int requestedBy = UserId,
            int approverId = ApproverStaffId,
            string actionType = "CASH_DIFFERENCE",
            string targetType = "shifts",
            int? targetId = null,
            string status = "Approved",
            decimal actualEndingCash = HighDiscrepancyActual,
            string reason = DefaultReason)
        {
            return new OtpChallenge
            {
                OtpChallengeId = 1,
                PublicId = ValidOtpPublicId,
                StoreId = storeId,
                WorkShiftId = shiftId,
                RequestedByStaffId = requestedBy,
                ApproverStaffId = approverId,
                ActionType = actionType,
                TargetType = targetType,
                TargetId = targetId,
                Reason = reason,
                PayloadFingerprint = Fingerprint.BuildCashDifferenceFingerprint(
                    storeId, requestedBy, shiftId, actualEndingCash, reason),
                OtpHash = "hashed",
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                Status = status,
                CreatedAt = DateTime.UtcNow,
                LastSentAt = DateTime.UtcNow,
                ApprovedAt = status == "Approved" ? DateTime.UtcNow : null
            };
        }

        private static Mock<IOtpChallengeRepository> SetupOtpRepo(OtpChallenge challenge, bool approverEligible = true)
        {
            var otpRepo = new Mock<IOtpChallengeRepository>(MockBehavior.Strict);
            otpRepo.Setup(r => r.BeginTransactionAsync()).Returns(Task.CompletedTask);
            otpRepo.Setup(r => r.CommitTransactionAsync()).Returns(Task.CompletedTask);
            otpRepo.Setup(r => r.RollbackTransactionAsync()).Returns(Task.CompletedTask);
            otpRepo.Setup(r => r.GetByPublicIdForUpdateAsync(ValidOtpPublicId))
                .ReturnsAsync(challenge);
            otpRepo.Setup(r => r.IsApproverStillEligibleAsync(
                    challenge.ApproverStaffId, It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(approverEligible);
            otpRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            return otpRepo;
        }

        private static WorkShiftService CreateService(
            Mock<IWorkShiftRepository> shiftRepo,
            Mock<IOtpChallengeRepository>? otpRepo = null,
            Mock<ISupervisorAuthService>? supervisorAuth = null)
        {
            return new WorkShiftService(
                shiftRepo.Object,
                Mock.Of<IHrAttendanceService>(),
                Mock.Of<IPOSOrderRepository>(),
                supervisorAuth?.Object ?? Mock.Of<ISupervisorAuthService>(),
                otpRepo?.Object ?? Mock.Of<IOtpChallengeRepository>(),
                Fingerprint,
                Mock.Of<ILogger<WorkShiftService>>());
        }

        private static Mock<IWorkShiftRepository> SetupShiftRepo(
            WorkShift shift,
            decimal totalCashSales = 0m)
        {
            var repo = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            repo.Setup(r => r.GetActiveShiftAsync(UserId, StoreId))
                .ReturnsAsync(shift);
            repo.Setup(r => r.HasOpenPosPaymentAsync(shift.ShiftId, StoreId))
                .ReturnsAsync(false);
            repo.Setup(r => r.GetTotalCashSalesAsync(shift.ShiftId))
                .ReturnsAsync(totalCashSales);
            repo.Setup(r => r.UpdateShiftAsync(It.IsAny<WorkShift>()))
                .Returns(Task.CompletedTask);
            return repo;
        }

        [Fact]
        public async Task CloseShift_DiscrepancyWithinThreshold_NoOtpRequired()
        {
            var shift = CreateOpenShift();
            var shiftRepo = SetupShiftRepo(shift);
            var service = CreateService(shiftRepo);

            var result = await service.CloseShiftAsync(UserId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = 490_000m,
                DiscrepancyReason = "Thiếu tiền lẻ."
            });

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("Closed", shift.Status);
        }

        [Fact]
        public async Task CloseShift_DiscrepancyWithinThreshold_RequiresReasonNotOtp()
        {
            var shift = CreateOpenShift();
            var shiftRepo = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            shiftRepo.Setup(r => r.GetActiveShiftAsync(UserId, StoreId))
                .ReturnsAsync(shift);
            shiftRepo.Setup(r => r.HasOpenPosPaymentAsync(shift.ShiftId, StoreId))
                .ReturnsAsync(false);
            shiftRepo.Setup(r => r.GetTotalCashSalesAsync(shift.ShiftId))
                .ReturnsAsync(0m);
            var service = CreateService(shiftRepo);

            var result = await service.CloseShiftAsync(UserId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = 490_000m
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("lý do chênh lệch", result.Message);
            Assert.NotEqual("Closed", shift.Status);
        }

        [Fact]
        public async Task CloseShift_Over50KDiscrepancy_NoOtp_ReturnsOtpRequired()
        {
            var shift = CreateOpenShift();
            var shiftRepo = SetupShiftRepo(shift);
            var service = CreateService(shiftRepo);

            var result = await service.CloseShiftAsync(UserId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = HighDiscrepancyActual,
                DiscrepancyReason = DefaultReason
            });

            Assert.False(result.IsSuccess);
            Assert.Equal("OTP_REQUIRED", result.ErrorCode);
            Assert.Contains("Cần xác nhận OTP", result.Message);
            Assert.NotEqual("Closed", shift.Status);
        }

        [Fact]
        public async Task CloseShift_Over2PercentDiscrepancy_NoOtp_ReturnsOtpRequired()
        {
            var shift = CreateOpenShift();
            var shiftRepo = SetupShiftRepo(shift, totalCashSales: 500_000m);
            var service = CreateService(shiftRepo);

            var result = await service.CloseShiftAsync(UserId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = 970_000m,
                DiscrepancyReason = "Tiền mặt thực tế ít hơn."
            });

            Assert.False(result.IsSuccess);
            Assert.Equal("OTP_REQUIRED", result.ErrorCode);
        }

        [Fact]
        public async Task CloseShift_WithApprovedOtp_AllowsClose()
        {
            var shift = CreateOpenShift();
            var shiftRepo = SetupShiftRepo(shift);
            var challenge = CreateApprovedChallenge();
            var otpRepo = SetupOtpRepo(challenge);
            var service = CreateService(shiftRepo, otpRepo);

            var result = await service.CloseShiftAsync(UserId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = HighDiscrepancyActual,
                DiscrepancyReason = DefaultReason,
                OtpChallengePublicId = ValidOtpPublicId
            });

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("Closed", shift.Status);
            otpRepo.Verify(r => r.CommitTransactionAsync(), Times.Once);
        }

        [Fact]
        public async Task CloseShift_WithOtp_MarksOtpUsed()
        {
            var shift = CreateOpenShift();
            var shiftRepo = SetupShiftRepo(shift);
            var challenge = CreateApprovedChallenge();
            var otpRepo = SetupOtpRepo(challenge);
            var service = CreateService(shiftRepo, otpRepo);

            var result = await service.CloseShiftAsync(UserId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = HighDiscrepancyActual,
                DiscrepancyReason = DefaultReason,
                OtpChallengePublicId = ValidOtpPublicId
            });

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(OtpConstants.Statuses.Used, challenge.Status);
            Assert.NotNull(challenge.UsedAt);
        }

        [Fact]
        public async Task CloseShift_WithUsedOtp_Rejected()
        {
            var shift = CreateOpenShift();
            var shiftRepo = SetupShiftRepo(shift);
            var challenge = CreateApprovedChallenge(status: OtpConstants.Statuses.Used);
            challenge.UsedAt = DateTime.UtcNow;
            var otpRepo = SetupOtpRepo(challenge);
            var service = CreateService(shiftRepo, otpRepo);

            var result = await service.CloseShiftAsync(UserId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = HighDiscrepancyActual,
                DiscrepancyReason = DefaultReason,
                OtpChallengePublicId = ValidOtpPublicId
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("đã được sử dụng", result.Message);
            otpRepo.Verify(r => r.RollbackTransactionAsync(), Times.Once);
        }

        [Fact]
        public async Task CloseShift_OtpForDifferentShift_Rejected()
        {
            var shift = CreateOpenShift();
            var shiftRepo = SetupShiftRepo(shift);
            var challenge = CreateApprovedChallenge(shiftId: 999);
            var otpRepo = SetupOtpRepo(challenge);
            var service = CreateService(shiftRepo, otpRepo);

            var result = await service.CloseShiftAsync(UserId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = HighDiscrepancyActual,
                DiscrepancyReason = DefaultReason,
                OtpChallengePublicId = ValidOtpPublicId
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("ca két tiền hiện tại", result.Message);
        }

        [Fact]
        public async Task CloseShift_OtpForDifferentStore_Rejected()
        {
            var shift = CreateOpenShift();
            var shiftRepo = SetupShiftRepo(shift);
            var challenge = CreateApprovedChallenge(storeId: 999);
            var otpRepo = SetupOtpRepo(challenge);
            var service = CreateService(shiftRepo, otpRepo);

            var result = await service.CloseShiftAsync(UserId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = HighDiscrepancyActual,
                DiscrepancyReason = DefaultReason,
                OtpChallengePublicId = ValidOtpPublicId
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("chi nhánh hiện tại", result.Message);
        }

        [Fact]
        public async Task CloseShift_OtpWithMismatchedTargetId_Rejected()
        {
            var shift = CreateOpenShift();
            var shiftRepo = SetupShiftRepo(shift);
            var challenge = CreateApprovedChallenge(targetId: 888);
            var otpRepo = SetupOtpRepo(challenge);
            var service = CreateService(shiftRepo, otpRepo);

            var result = await service.CloseShiftAsync(UserId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = HighDiscrepancyActual,
                DiscrepancyReason = DefaultReason,
                OtpChallengePublicId = ValidOtpPublicId
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("không khớp ca hiện tại", result.Message);
        }

        [Fact]
        public async Task CloseShift_PayloadCashMismatch_Rejected()
        {
            var shift = CreateOpenShift();
            var shiftRepo = SetupShiftRepo(shift);
            var challenge = CreateApprovedChallenge(actualEndingCash: 430_000m);
            var otpRepo = SetupOtpRepo(challenge);
            var service = CreateService(shiftRepo, otpRepo);

            var result = await service.CloseShiftAsync(UserId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = HighDiscrepancyActual,
                DiscrepancyReason = DefaultReason,
                OtpChallengePublicId = ValidOtpPublicId
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.ErrorCodes.PayloadMismatch, result.ErrorCode);
        }

        [Fact]
        public async Task CloseShift_SelfApproval_Rejected()
        {
            var shift = CreateOpenShift();
            var shiftRepo = SetupShiftRepo(shift);
            var challenge = CreateApprovedChallenge(approverId: UserId);
            var otpRepo = SetupOtpRepo(challenge);
            var service = CreateService(shiftRepo, otpRepo);

            var result = await service.CloseShiftAsync(UserId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = HighDiscrepancyActual,
                DiscrepancyReason = DefaultReason,
                OtpChallengePublicId = ValidOtpPublicId
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.ErrorCodes.NoEligibleApprover, result.ErrorCode);
        }

        [Fact]
        public async Task CloseShift_InactiveApprover_Rejected()
        {
            var shift = CreateOpenShift();
            var shiftRepo = SetupShiftRepo(shift);
            var challenge = CreateApprovedChallenge();
            var otpRepo = SetupOtpRepo(challenge, approverEligible: false);
            var service = CreateService(shiftRepo, otpRepo);

            var result = await service.CloseShiftAsync(UserId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = HighDiscrepancyActual,
                DiscrepancyReason = DefaultReason,
                OtpChallengePublicId = ValidOtpPublicId
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.ErrorCodes.ApproverNoLongerEligible, result.ErrorCode);
        }

        [Fact]
        public async Task CloseShift_WhenUpdateFails_RollsBackAndDoesNotCommitUsed()
        {
            var shift = CreateOpenShift();
            var shiftRepo = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            shiftRepo.Setup(r => r.GetActiveShiftAsync(UserId, StoreId))
                .ReturnsAsync(shift);
            shiftRepo.Setup(r => r.HasOpenPosPaymentAsync(shift.ShiftId, StoreId))
                .ReturnsAsync(false);
            shiftRepo.Setup(r => r.GetTotalCashSalesAsync(shift.ShiftId))
                .ReturnsAsync(0m);
            shiftRepo.Setup(r => r.UpdateShiftAsync(It.IsAny<WorkShift>()))
                .ThrowsAsync(new Exception("DB connection lost"));

            var challenge = CreateApprovedChallenge();
            var otpRepo = SetupOtpRepo(challenge);
            var service = CreateService(shiftRepo, otpRepo);

            var result = await service.CloseShiftAsync(UserId, StoreId, new CloseShiftRequestDto
            {
                ActualEndingCash = HighDiscrepancyActual,
                DiscrepancyReason = DefaultReason,
                OtpChallengePublicId = ValidOtpPublicId
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("Lỗi hệ thống", result.Message);
            otpRepo.Verify(r => r.RollbackTransactionAsync(), Times.Once);
            otpRepo.Verify(r => r.CommitTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task CloseShiftByException_WithLegacyPin_IsRejected()
        {
            var shift = CreateOpenShift();
            var shiftRepo = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            shiftRepo.Setup(r => r.GetActiveShiftAsync(UserId, StoreId))
                .ReturnsAsync(shift);

            var service = CreateService(shiftRepo);

            var result = await service.CloseShiftByExceptionAsync(UserId, StoreId, ShiftId,
                new CloseShiftExceptionRequestDto
                {
                    ActualEndingCash = HighDiscrepancyActual,
                    DiscrepancyReason = "Thiếu tiền mặt.",
                    ExceptionReason = "Mất mạng kéo dài.",
                    SupervisorPin = "9999"
                });

            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.ErrorCodes.FeatureNotAvailable, result.ErrorCode);
            Assert.NotEqual("Closed", shift.Status);
        }
    }
}
