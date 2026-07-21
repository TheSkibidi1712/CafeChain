using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Admin.Vouchers;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Services.PayOSIntegration;
using CafeChain.Application.Services.POS;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Drinks;
using CafeChain.Models.Operations;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Stores;
// Size lives under Models.Drinks
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>
    /// Issue #85 / Phase 2 (#141): exception close uses OTP, not SupervisorPin.
    /// </summary>
    public class POSWorkShiftCloseExceptionIssue85Tests
    {
        private static readonly Guid OtpId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly OtpPayloadFingerprintService Fingerprint = new();

        [Fact]
        public async Task CloseShiftByExceptionAsync_MissingReason_IsRejectedBeforeMutatingShift()
        {
            var repository = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            var service = CreateWorkShiftService(repository, Mock.Of<IOtpChallengeRepository>());

            var result = await service.CloseShiftByExceptionAsync(17, 3, 85, new CloseShiftExceptionRequestDto
            {
                ActualEndingCash = 500000m,
                OtpChallengePublicId = OtpId
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("lý do đóng ca ngoại lệ", result.Message);
            repository.Verify(repo => repo.UpdateShiftAsync(It.IsAny<WorkShift>()), Times.Never);
        }

        [Fact]
        public void CloseShiftExceptionRequestDto_HasNoLegacyPinFields()
        {
            Assert.Null(typeof(CloseShiftExceptionRequestDto).GetProperty("SupervisorPin"));
            Assert.Null(typeof(CloseShiftExceptionRequestDto).GetProperty("Pin"));
            Assert.Null(typeof(CloseShiftExceptionRequestDto).GetProperty("PinCode"));
        }

        [Fact]
        public async Task CloseShiftByExceptionAsync_BackendAwaitingPaymentStillBlocksExceptionClose()
        {
            var shift = CreateOpenShift();
            var repository = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            repository.Setup(repo => repo.GetActiveShiftAsync(17, 3)).ReturnsAsync(shift);
            repository.Setup(repo => repo.HasOpenPosPaymentAsync(85, 3)).ReturnsAsync(true);
            var service = CreateWorkShiftService(repository, Mock.Of<IOtpChallengeRepository>());

            var result = await service.CloseShiftByExceptionAsync(17, 3, 85, CreateExceptionRequestWithOtp());

            Assert.False(result.IsSuccess);
            Assert.Contains("giao dịch thanh toán chưa hoàn tất", result.Message);
            Assert.Equal("Open", shift.Status);
            repository.Verify(repo => repo.UpdateShiftAsync(It.IsAny<WorkShift>()), Times.Never);
        }

        [Fact]
        public async Task CloseShiftException_ValidOtp_ClosesShift()
        {
            var shift = CreateOpenShift();
            var repository = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            repository.Setup(repo => repo.GetActiveShiftAsync(17, 3)).ReturnsAsync(shift);
            repository.Setup(repo => repo.HasOpenPosPaymentAsync(85, 3)).ReturnsAsync(false);
            repository.Setup(repo => repo.GetTotalCashSalesAsync(85)).ReturnsAsync(0m);
            repository.Setup(repo => repo.UpdateShiftAsync(It.IsAny<WorkShift>())).Returns(Task.CompletedTask);

            var challenge = CreateApprovedExceptionChallenge();
            var otpRepo = SetupOtpRepo(challenge);
            var posRepo = new Mock<IPOSOrderRepository>(MockBehavior.Loose);
            posRepo.Setup(r => r.CreateAuditLogAsync(It.IsAny<InvoiceAuditLog>())).Returns(Task.CompletedTask);

            var service = CreateWorkShiftService(repository, otpRepo.Object, posRepo.Object);
            var result = await service.CloseShiftByExceptionAsync(17, 3, 85, CreateExceptionRequestWithOtp());

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("Closed", shift.Status);
            Assert.True(shift.IsExceptionClosed);
            Assert.True(shift.RequiresReconciliation);
            Assert.Equal(200, shift.ExceptionClosedByStaffId);
            Assert.Equal(OtpConstants.Statuses.Used, challenge.Status);
            repository.Verify(repo => repo.UpdateShiftAsync(shift), Times.Once);
            otpRepo.Verify(r => r.CommitTransactionAsync(), Times.Once);
        }

        [Fact]
        public async Task CommitOfflineSyncedOrderAsync_LateSyncMarksReconciliationWithoutChangingActualEndingCash()
        {
            var repository = new Mock<IPOSOrderRepository>(MockBehavior.Strict);
            var workShiftService = new Mock<IWorkShiftService>(MockBehavior.Strict);
            var clientOrderId = Guid.NewGuid();
            var dto = CreateOfflineCashCommitDto(clientOrderId);
            var closedShift = CreateClosedShift();

            repository.Setup(repo => repo.FindOrderByClientOrderIdAsync(clientOrderId)).ReturnsAsync((Order?)null);
            workShiftService.Setup(service => service.GetShiftByIdAsync(42, 17, 3)).ReturnsAsync(closedShift);
            repository.Setup(repo => repo.BeginTransactionAsync()).Returns(Task.CompletedTask);
            repository.Setup(repo => repo.GetDrinkWithSizesAsync(10, 3)).ReturnsAsync(CreateDrink());
            repository.Setup(repo => repo.CreateOrderAsync(It.IsAny<Order>()))
                .Callback<Order>(order => order.OrderId = 701)
                .ReturnsAsync((Order order) => order);
            repository.Setup(repo => repo.CreatePaymentAsync(It.IsAny<Payment>())).Returns(Task.CompletedTask);
            repository.Setup(repo => repo.SaveChangesAsync()).Returns(Task.CompletedTask);
            repository.Setup(repo => repo.CommitTransactionAsync()).Returns(Task.CompletedTask);
            var service = CreateOrderService(repository, workShiftService);

            var result = await service.CommitOfflineSyncedOrderAsync(dto, 17, 3, 42, DateTime.Now);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(500000m, closedShift.ActualEndingCash);
            Assert.True(closedShift.RequiresReconciliation);
            Assert.True(closedShift.HasLateOfflineSync);
            Assert.Equal(1, closedShift.LateOfflineSyncCount);
            Assert.NotNull(closedShift.LastLateOfflineSyncedAt);
        }

        [Fact]
        public async Task CommitOfflineSyncedOrderAsync_DuplicateSyncDoesNotIncrementLateOfflineSyncCount()
        {
            var repository = new Mock<IPOSOrderRepository>(MockBehavior.Strict);
            var workShiftService = new Mock<IWorkShiftService>(MockBehavior.Strict);
            var clientOrderId = Guid.NewGuid();
            var dto = CreateOfflineCashCommitDto(clientOrderId);
            repository.Setup(repo => repo.FindOrderByClientOrderIdAsync(clientOrderId)).ReturnsAsync(new Order
            {
                OrderId = 702,
                ClientOrderId = clientOrderId,
                WorkShiftId = 42,
                SubTotal = 45000m,
                Total = 45000m
            });
            var service = CreateOrderService(repository, workShiftService);

            var result = await service.CommitOfflineSyncedOrderAsync(dto, 17, 3, 42, DateTime.Now);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(true, result.Data!.GetType().GetProperty("isIdempotent")?.GetValue(result.Data));
            workShiftService.Verify(service => service.GetShiftByIdAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            repository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
        }

        private static WorkShiftService CreateWorkShiftService(
            Mock<IWorkShiftRepository> repository,
            IOtpChallengeRepository otpRepo,
            IPOSOrderRepository? posRepo = null)
        {
            return new WorkShiftService(
                repository.Object,
                posRepo ?? Mock.Of<IPOSOrderRepository>(),
                otpRepo,
                Fingerprint,
                Mock.Of<ILogger<WorkShiftService>>());
        }

        private static Mock<IOtpChallengeRepository> SetupOtpRepo(OtpChallenge challenge)
        {
            var otpRepo = new Mock<IOtpChallengeRepository>(MockBehavior.Strict);
            otpRepo.Setup(r => r.BeginTransactionAsync()).Returns(Task.CompletedTask);
            otpRepo.Setup(r => r.CommitTransactionAsync()).Returns(Task.CompletedTask);
            otpRepo.Setup(r => r.RollbackTransactionAsync()).Returns(Task.CompletedTask);
            otpRepo.Setup(r => r.GetByPublicIdForUpdateAsync(OtpId)).ReturnsAsync(challenge);
            otpRepo.Setup(r => r.IsApproverStillEligibleAsync(200, 3, 17)).ReturnsAsync(true);
            otpRepo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            return otpRepo;
        }

        private static OtpChallenge CreateApprovedExceptionChallenge()
        {
            const string reason = "Mất mạng kéo dài, còn đơn offline chưa sync.";
            var offline = new OfflineQueueSummaryDto
            {
                OfflineOrderCount = 2,
                EstimatedTotal = 90000m,
                LocalCashTotal = 90000m
            };
            return new OtpChallenge
            {
                PublicId = OtpId,
                StoreId = 3,
                WorkShiftId = 85,
                RequestedByStaffId = 17,
                ApproverStaffId = 200,
                ActionType = OtpConstants.ActionTypes.CloseShiftException,
                TargetType = OtpConstants.TargetTypes.Shifts,
                TargetId = 85,
                Reason = reason,
                PayloadFingerprint = Fingerprint.BuildCloseShiftExceptionFingerprint(
                    3, 17, 85, 500000m, reason, null, offline),
                OtpHash = "x",
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                Status = OtpConstants.Statuses.Approved,
                ApprovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                LastSentAt = DateTime.UtcNow
            };
        }

        private static POSOrderService CreateOrderService(
            Mock<IPOSOrderRepository> repository,
            Mock<IWorkShiftService> workShiftService)
        {
            return new POSOrderService(
                repository.Object,
                workShiftService.Object,
                Mock.Of<IAdminVoucherService>(),
                Mock.Of<IPrintDispatcher>(),
                Mock.Of<IPayOSService>(),
                Mock.Of<ILogger<POSOrderService>>());
        }

        private static CloseShiftExceptionRequestDto CreateExceptionRequestWithOtp()
        {
            return new CloseShiftExceptionRequestDto
            {
                ActualEndingCash = 500000m,
                ExceptionReason = "Mất mạng kéo dài, còn đơn offline chưa sync.",
                OtpChallengePublicId = OtpId,
                OfflineQueueSummary = new OfflineQueueSummaryDto
                {
                    OfflineOrderCount = 2,
                    EstimatedTotal = 90000m,
                    LocalCashTotal = 90000m
                }
            };
        }

        private static WorkShift CreateOpenShift()
        {
            return new WorkShift
            {
                ShiftId = 85,
                StoreId = 3,
                UserId = 17,
                Status = "Open",
                StartingCash = 500000m,
                ExpectedEndingCash = 500000m
            };
        }

        private static WorkShift CreateClosedShift()
        {
            return new WorkShift
            {
                ShiftId = 42,
                StoreId = 3,
                UserId = 17,
                Status = "Closed",
                StartingCash = 500000m,
                ExpectedEndingCash = 500000m,
                ActualEndingCash = 500000m,
                IsExceptionClosed = true,
                RequiresReconciliation = true
            };
        }

        private static Drink CreateDrink()
        {
            return new Drink
            {
                DrinkId = 10,
                Name = "Americano",
                DrinkSizes = new List<DrinkSize>
                {
                    new()
                    {
                        DrinkId = 10,
                        SizeId = 2,
                        Price = 45000m,
                        Active = true,
                        Size = new Size
                        {
                            SizeId = 2,
                            Name = "M",
                            Active = true
                        }
                    }
                }
            };
        }

        private static POSOrderCommitDto CreateOfflineCashCommitDto(Guid clientOrderId)
        {
            return new POSOrderCommitDto
            {
                ClientOrderId = clientOrderId,
                Items = new List<POSOrderItemDto>
                {
                    new()
                    {
                        DrinkId = 10,
                        SizeId = 2,
                        Quantity = 1,
                        Toppings = new List<POSOrderToppingDto>()
                    }
                },
                Payments = new List<PaymentLineDto>
                {
                    new() { PaymentMethodId = 1, Amount = 45000m }
                },
                PaymentMethodId = 1,
                ReceivedAmount = 50000m,
                OrderTypeId = 1,
                SkipPrint = true
            };
        }
    }
}
