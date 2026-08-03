using System;
using System.Globalization;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Services.POS;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Operations;
using CafeChain.Models.Orders;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
// StaffShift + Shift: CafeChain.Models.Staffs
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>
    /// Phase 2 (#141): CLOSE_SHIFT_EXCEPTION + OPEN_SHIFT_LATE OTP bindings.
    /// </summary>
    public class POSOtpSecurityPhase2Tests
    {
        private const int UserId = 17;
        private const int StoreId = 3;
        private const int ShiftId = 141;
        private const int ApproverId = 200;
        private static readonly Guid OtpId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly OtpPayloadFingerprintService Fp = new();

        // ---------- Close exception ----------

        [Fact]
        public async Task CloseShiftException_RequiresOtpChallenge_NotSupervisorPin()
        {
            var service = CreateCloseService(CreateOpenShift(), out _, out _);
            var result = await service.CloseShiftByExceptionAsync(UserId, StoreId, ShiftId,
                new CloseShiftExceptionRequestDto
                {
                    ActualEndingCash = 500_000m,
                    ExceptionReason = "Mất mạng",
                    OfflineQueueSummary = DefaultOffline()
                });

            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.ErrorCodes.Required, result.ErrorCode);
        }

        [Fact]
        public void CloseShiftException_HasNoLegacySupervisorPinDtoField()
        {
            Assert.Null(typeof(CloseShiftExceptionRequestDto).GetProperty("SupervisorPin"));
            Assert.Null(typeof(CloseShiftRequestDto).GetProperty("SupervisorPin"));
        }

        [Fact]
        public async Task CloseShiftException_ValidOtp_ClosesShift()
        {
            var shift = CreateOpenShift();
            var challenge = ApprovedExceptionChallenge(500_000m, "Mất mạng", DefaultOffline());
            var service = CreateCloseService(shift, out var otpRepo, out _, challenge);
            var result = await service.CloseShiftByExceptionAsync(UserId, StoreId, ShiftId, ExceptionRequest(500_000m, "Mất mạng"));

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(WorkShiftStatuses.ReconciliationRequired, shift.Status);
            Assert.True(shift.IsExceptionClosed);
            Assert.Equal(ApproverId, shift.ExceptionClosedByStaffId);
            Assert.Equal(OtpConstants.Statuses.Used, challenge.Status);
            otpRepo.Verify(r => r.CommitTransactionAsync(), Times.Once);
        }

        [Fact]
        public async Task CloseShiftException_SetsApproverAsExceptionClosedByStaff()
        {
            var shift = CreateOpenShift();
            var challenge = ApprovedExceptionChallenge(500_000m, "Mất mạng", DefaultOffline());
            var service = CreateCloseService(shift, out _, out _, challenge);
            await service.CloseShiftByExceptionAsync(UserId, StoreId, ShiftId, ExceptionRequest(500_000m, "Mất mạng"));
            Assert.Equal(ApproverId, shift.ExceptionClosedByStaffId);
        }

        [Fact]
        public async Task CloseShiftException_ChangedCash_IsRejected()
        {
            var challenge = ApprovedExceptionChallenge(500_000m, "Mất mạng", DefaultOffline());
            var service = CreateCloseService(CreateOpenShift(), out _, out _, challenge);
            var req = ExceptionRequest(490_000m, "Mất mạng");
            req.DiscrepancyReason = "Lệch khi test fingerprint";
            var result = await service.CloseShiftByExceptionAsync(UserId, StoreId, ShiftId, req);
            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.ErrorCodes.PayloadMismatch, result.ErrorCode);
        }

        [Fact]
        public async Task CloseShiftException_ChangedReason_IsRejected()
        {
            var challenge = ApprovedExceptionChallenge(500_000m, "Mất mạng", DefaultOffline());
            var service = CreateCloseService(CreateOpenShift(), out _, out _, challenge);
            var result = await service.CloseShiftByExceptionAsync(UserId, StoreId, ShiftId, ExceptionRequest(500_000m, "Lý do khác"));
            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.ErrorCodes.PayloadMismatch, result.ErrorCode);
        }

        [Fact]
        public async Task CloseShiftException_ChangedOfflineSummary_IsRejected()
        {
            var offline = DefaultOffline();
            var challenge = ApprovedExceptionChallenge(500_000m, "Mất mạng", offline);
            var service = CreateCloseService(CreateOpenShift(), out _, out _, challenge);
            var req = ExceptionRequest(500_000m, "Mất mạng");
            req.OfflineQueueSummary = new OfflineQueueSummaryDto
            {
                OfflineOrderCount = 9,
                EstimatedTotal = 90000m,
                LocalCashTotal = 90000m
            };
            var result = await service.CloseShiftByExceptionAsync(UserId, StoreId, ShiftId, req);
            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.ErrorCodes.PayloadMismatch, result.ErrorCode);
        }

        [Fact]
        public async Task CloseShiftException_Replay_IsRejected()
        {
            var shift = CreateOpenShift();
            var challenge = ApprovedExceptionChallenge(500_000m, "Mất mạng", DefaultOffline());
            var service = CreateCloseService(shift, out _, out _, challenge);
            var first = await service.CloseShiftByExceptionAsync(UserId, StoreId, ShiftId, ExceptionRequest(500_000m, "Mất mạng"));
            Assert.True(first.IsSuccess, first.Message);

            shift.Status = "Open";
            challenge.Status = OtpConstants.Statuses.Used;
            var second = await service.CloseShiftByExceptionAsync(UserId, StoreId, ShiftId, ExceptionRequest(500_000m, "Mất mạng"));
            Assert.False(second.IsSuccess);
            Assert.Contains("đã được sử dụng", second.Message);
        }

        [Fact]
        public async Task CloseShiftException_MutationFailure_DoesNotConsumeChallenge()
        {
            var shift = CreateOpenShift();
            var challenge = ApprovedExceptionChallenge(500_000m, "Mất mạng", DefaultOffline());
            var shiftRepo = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            shiftRepo.Setup(r => r.GetActiveShiftAsync(UserId, StoreId)).ReturnsAsync(shift);
            shiftRepo.Setup(r => r.HasOpenPosPaymentAsync(ShiftId, StoreId)).ReturnsAsync(false);
            shiftRepo.Setup(r => r.GetTotalCashSalesAsync(ShiftId)).ReturnsAsync(0m);
            shiftRepo.Setup(r => r.UpdateShiftAsync(It.IsAny<WorkShift>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            var otpRepo = SetupOtp(challenge);
            var service = new WorkShiftService(
                shiftRepo.Object,
                Mock.Of<IPOSOrderRepository>(),
                otpRepo.Object,
                Fp,
                Mock.Of<ILogger<WorkShiftService>>());

            var result = await service.CloseShiftByExceptionAsync(UserId, StoreId, ShiftId, ExceptionRequest(500_000m, "Mất mạng"));
            Assert.False(result.IsSuccess);
            otpRepo.Verify(r => r.RollbackTransactionAsync(), Times.Once);
            otpRepo.Verify(r => r.CommitTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task CloseShiftException_OtpApprover_CannotBeActor()
        {
            var challenge = ApprovedExceptionChallenge(500_000m, "Mất mạng", DefaultOffline());
            challenge.ApproverStaffId = UserId;
            var service = CreateCloseService(CreateOpenShift(), out _, out _, challenge);
            var result = await service.CloseShiftByExceptionAsync(UserId, StoreId, ShiftId, ExceptionRequest(500_000m, "Mất mạng"));
            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.ErrorCodes.NoEligibleApprover, result.ErrorCode);
        }

        // ---------- Open late ----------

        [Fact]
        public async Task OpenShift_WithoutScheduledStaffShift_RequiresOutsideScheduleFlow()
        {
            var service = CreateOpenService(isLate: false, out var shiftRepo, out _);
            var result = await service.OpenShiftAsync(UserId, StoreId, new OpenShiftRequestDto
            {
                StartingCash = 500_000m
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(WorkShiftErrorCodes.OutsideScheduleReasonRequired, result.ErrorCode);
            shiftRepo.Verify(r => r.CreateShiftAsync(It.IsAny<WorkShift>()), Times.Never);
        }

        [Fact]
        public async Task OpenShiftLate_RequiresOtpChallenge_NotRecentPinAudit()
        {
            var service = CreateOpenService(isLate: true, out _, out var posRepo);
            posRepo.Setup(r => r.GetPendingAuditLogAsync(UserId, "OPEN_SHIFT_LATE", 5))
                .ReturnsAsync(new InvoiceAuditLog { Id = 1, ActionName = "OPEN_SHIFT_LATE", CashierId = UserId });

            var result = await service.OpenShiftAsync(UserId, StoreId, new OpenShiftRequestDto
            {
                StartingCash = 500_000m,
                LateOpeningReason = "Tac duong den ca tre"
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.ErrorCodes.LateOpeningRequiresOtp, result.ErrorCode);
            // Recent audit must NOT authorize
            posRepo.Verify(r => r.GetPendingAuditLogAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task OpenShiftLate_RecentInvoiceAuditLog_DoesNotAuthorize()
        {
            await OpenShiftLate_RequiresOtpChallenge_NotRecentPinAudit();
        }

        private static string LateScheduledCanonical()
            => DateTime.Today.Add(TimeSpan.FromHours(0))
                .ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

        [Fact]
        public async Task OpenShiftLate_ValidOtp_OpensShift()
        {
            var challenge = ApprovedOpenLateChallenge(500_000m, "Tac duong den ca tre", LateScheduledCanonical());
            var audit = new Mock<IWorkShiftAuditService>();
            var service = CreateOpenService(isLate: true, out var shiftRepo, out var posRepo, challenge, audit);
            shiftRepo.Setup(r => r.CreateShiftAsync(It.IsAny<WorkShift>()))
                .Callback<WorkShift>(s => s.ShiftId = 999)
                .ReturnsAsync((WorkShift s) => s);
            posRepo.Setup(r => r.CreateAuditLogAsync(It.IsAny<InvoiceAuditLog>())).Returns(Task.CompletedTask);

            var result = await service.OpenShiftAsync(UserId, StoreId, new OpenShiftRequestDto
            {
                StartingCash = 500_000m,
                LateOpeningReason = "Tac duong den ca tre",
                OtpChallengePublicId = OtpId
            });

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(OtpConstants.Statuses.Used, challenge.Status);
            audit.Verify(a => a.WriteAsync(
                "WORKSHIFT_OPENED",
                999,
                UserId,
                null,
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task OpenShiftLate_ChangedReason_IsRejected()
        {
            var challenge = ApprovedOpenLateChallenge(500_000m, "Tac duong den ca tre", LateScheduledCanonical());
            var service = CreateOpenService(isLate: true, out _, out _, challenge);
            var result = await service.OpenShiftAsync(UserId, StoreId, new OpenShiftRequestDto
            {
                StartingCash = 500_000m,
                LateOpeningReason = "Lý do khác",
                OtpChallengePublicId = OtpId
            });
            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.ErrorCodes.PayloadMismatch, result.ErrorCode);
        }

        [Fact]
        public async Task OpenShiftLate_OtpApprover_CannotBeActor()
        {
            var challenge = ApprovedOpenLateChallenge(500_000m, "Tac duong den ca tre", LateScheduledCanonical());
            challenge.ApproverStaffId = UserId;
            var service = CreateOpenService(isLate: true, out _, out _, challenge);
            var result = await service.OpenShiftAsync(UserId, StoreId, new OpenShiftRequestDto
            {
                StartingCash = 500_000m,
                LateOpeningReason = "Tac duong den ca tre",
                OtpChallengePublicId = OtpId
            });
            Assert.False(result.IsSuccess);
            Assert.Equal(OtpConstants.ErrorCodes.NoEligibleApprover, result.ErrorCode);
        }

        [Fact]
        public async Task OpenShiftLate_WrongActor_IsRejected()
        {
            var challenge = ApprovedOpenLateChallenge(500_000m, "Tac duong den ca tre", LateScheduledCanonical());
            challenge.RequestedByStaffId = 999;
            var service = CreateOpenService(isLate: true, out _, out _, challenge);
            var result = await service.OpenShiftAsync(UserId, StoreId, new OpenShiftRequestDto
            {
                StartingCash = 500_000m,
                LateOpeningReason = "Tac duong den ca tre",
                OtpChallengePublicId = OtpId
            });
            Assert.False(result.IsSuccess);
            Assert.Contains("không thuộc nhân viên", result.Message);
        }

        [Fact]
        public async Task OpenShiftLate_WrongStore_IsRejected()
        {
            var challenge = ApprovedOpenLateChallenge(500_000m, "Tac duong den ca tre", LateScheduledCanonical());
            challenge.StoreId = 99;
            var service = CreateOpenService(isLate: true, out _, out _, challenge);
            var result = await service.OpenShiftAsync(UserId, StoreId, new OpenShiftRequestDto
            {
                StartingCash = 500_000m,
                LateOpeningReason = "Tac duong den ca tre",
                OtpChallengePublicId = OtpId
            });
            Assert.False(result.IsSuccess);
            Assert.Contains("chi nhánh", result.Message);
        }

        [Fact]
        public async Task OpenShiftLate_Replay_IsRejected()
        {
            var challenge = ApprovedOpenLateChallenge(500_000m, "Tac duong den ca tre", LateScheduledCanonical());
            challenge.Status = OtpConstants.Statuses.Used;
            var service = CreateOpenService(isLate: true, out _, out _, challenge);
            var result = await service.OpenShiftAsync(UserId, StoreId, new OpenShiftRequestDto
            {
                StartingCash = 500_000m,
                LateOpeningReason = "Tac duong den ca tre",
                OtpChallengePublicId = OtpId
            });
            Assert.False(result.IsSuccess);
            Assert.Contains("đã được sử dụng", result.Message);
        }

        [Fact]
        public async Task OpenShiftLate_MutationFailure_DoesNotConsumeChallenge()
        {
            var challenge = ApprovedOpenLateChallenge(500_000m, "Tac duong den ca tre", LateScheduledCanonical());
            var service = CreateOpenService(isLate: true, out var shiftRepo, out _, challenge);
            shiftRepo.Setup(r => r.CreateShiftAsync(It.IsAny<WorkShift>()))
                .ThrowsAsync(new InvalidOperationException("insert fail"));

            var result = await service.OpenShiftAsync(UserId, StoreId, new OpenShiftRequestDto
            {
                StartingCash = 500_000m,
                LateOpeningReason = "Tac duong den ca tre",
                OtpChallengePublicId = OtpId
            });

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task OpenShiftLate_WritesAuditAfterSuccessfulConsume()
        {
            await OpenShiftLate_ValidOtp_OpensShift();
        }

        // ---------- helpers ----------

        private static OfflineQueueSummaryDto DefaultOffline() => new()
        {
            OfflineOrderCount = 2,
            EstimatedTotal = 90_000m,
            LocalCashTotal = 90_000m
        };

        private static CloseShiftExceptionRequestDto ExceptionRequest(decimal cash, string reason) => new()
        {
            ActualEndingCash = cash,
            ExceptionReason = reason,
            OtpChallengePublicId = OtpId,
            OfflineQueueSummary = DefaultOffline()
        };

        private static WorkShift CreateOpenShift() => new()
        {
            ShiftId = ShiftId,
            StoreId = StoreId,
            UserId = UserId,
            Status = "Open",
            StartingCash = 500_000m,
            ExpectedEndingCash = 500_000m
        };

        private static OtpChallenge ApprovedExceptionChallenge(decimal cash, string reason, OfflineQueueSummaryDto offline) => new()
        {
            PublicId = OtpId,
            StoreId = StoreId,
            WorkShiftId = ShiftId,
            RequestedByStaffId = UserId,
            ApproverStaffId = ApproverId,
            ActionType = OtpConstants.ActionTypes.CloseShiftException,
            TargetType = OtpConstants.TargetTypes.Shifts,
            TargetId = ShiftId,
            Reason = reason,
            PayloadFingerprint = Fp.BuildCloseShiftExceptionFingerprint(
                StoreId, UserId, ShiftId, cash, reason, null, offline),
            OtpHash = "x",
            Status = OtpConstants.Statuses.Approved,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            LastSentAt = DateTime.UtcNow
        };

        private static OtpChallenge ApprovedOpenLateChallenge(decimal startingCash, string reason, string scheduled) => new()
        {
            PublicId = OtpId,
            StoreId = StoreId,
            WorkShiftId = null,
            RequestedByStaffId = UserId,
            ApproverStaffId = ApproverId,
            ActionType = OtpConstants.ActionTypes.OpenShiftLate,
            TargetType = OtpConstants.TargetTypes.Shifts,
            TargetId = UserId,
            Reason = reason,
            PayloadFingerprint = Fp.BuildOpenShiftLateFingerprint(
                StoreId, UserId, startingCash, reason, scheduled),
            OtpHash = "x",
            Status = OtpConstants.Statuses.Approved,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            LastSentAt = DateTime.UtcNow
        };

        private static Mock<IOtpChallengeRepository> SetupOtp(OtpChallenge challenge)
        {
            var otp = new Mock<IOtpChallengeRepository>(MockBehavior.Strict);
            otp.Setup(r => r.BeginTransactionAsync()).Returns(Task.CompletedTask);
            otp.Setup(r => r.CommitTransactionAsync()).Returns(Task.CompletedTask);
            otp.Setup(r => r.RollbackTransactionAsync()).Returns(Task.CompletedTask);
            otp.Setup(r => r.GetByPublicIdForUpdateAsync(OtpId)).ReturnsAsync(challenge);
            otp.Setup(r => r.IsApproverStillEligibleAsync(It.IsAny<int>(), StoreId, UserId))
                .ReturnsAsync((int approver, int store, int actor) => approver != actor && challenge.ApproverStaffId == approver);
            otp.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            return otp;
        }

        private static WorkShiftService CreateCloseService(
            WorkShift shift,
            out Mock<IOtpChallengeRepository> otpRepo,
            out Mock<IPOSOrderRepository> posRepo,
            OtpChallenge? challenge = null)
        {
            var shiftRepo = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            shiftRepo.Setup(r => r.GetActiveShiftAsync(UserId, StoreId)).ReturnsAsync(shift);
            shiftRepo.Setup(r => r.HasOpenPosPaymentAsync(ShiftId, StoreId)).ReturnsAsync(false);
            shiftRepo.Setup(r => r.GetTotalCashSalesAsync(ShiftId)).ReturnsAsync(0m);
            shiftRepo.Setup(r => r.UpdateShiftAsync(It.IsAny<WorkShift>())).Returns(Task.CompletedTask);

            otpRepo = challenge != null ? SetupOtp(challenge) : new Mock<IOtpChallengeRepository>(MockBehavior.Loose);
            posRepo = new Mock<IPOSOrderRepository>(MockBehavior.Loose);
            posRepo.Setup(r => r.CreateAuditLogAsync(It.IsAny<InvoiceAuditLog>())).Returns(Task.CompletedTask);

            return new WorkShiftService(
                shiftRepo.Object,
                posRepo.Object,
                otpRepo.Object,
                Fp,
                Mock.Of<ILogger<WorkShiftService>>());
        }

        private static WorkShiftService CreateOpenService(
            bool isLate,
            out Mock<IWorkShiftRepository> shiftRepo,
            out Mock<IPOSOrderRepository> posRepo,
            OtpChallenge? challenge = null,
            Mock<IWorkShiftAuditService>? audit = null)
        {
            shiftRepo = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            shiftRepo.Setup(r => r.GetActiveShiftAsync(UserId, StoreId)).ReturnsAsync((WorkShift?)null);

            if (isLate)
            {
                shiftRepo.Setup(r => r.GetEffectiveStaffShiftAsync(UserId, StoreId, It.IsAny<DateTime>())).ReturnsAsync(new StaffShift
                {
                    StaffId = UserId,
                    WorkDate = DateTime.Today,
                    Shift = new Shift
                    {
                        // Start far in the past today so minutes late > 30
                        StartTime = TimeSpan.FromHours(0)
                    }
                });
            }
            else
            {
                shiftRepo.Setup(r => r.GetEffectiveStaffShiftAsync(UserId, StoreId, It.IsAny<DateTime>())).ReturnsAsync((StaffShift?)null);
            }

            shiftRepo.Setup(r => r.EnsurePosTerminalAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            shiftRepo.Setup(r => r.CreateShiftAsync(It.IsAny<WorkShift>()))
                .ReturnsAsync((WorkShift s) => s);

            posRepo = new Mock<IPOSOrderRepository>(MockBehavior.Loose);
            var otpRepo = challenge != null ? SetupOtp(challenge) : new Mock<IOtpChallengeRepository>(MockBehavior.Loose);

            return new WorkShiftService(
                shiftRepo.Object,
                posRepo.Object,
                otpRepo.Object,
                Fp,
                Mock.Of<ILogger<WorkShiftService>>(),
                audit: audit?.Object);
        }
    }
}
