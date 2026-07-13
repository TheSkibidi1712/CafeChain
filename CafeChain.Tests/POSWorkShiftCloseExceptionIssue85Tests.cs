using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Admin.Vouchers;
using CafeChain.Application.Interfaces.Attendance;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Application.Services.PayOSIntegration;
using CafeChain.Application.Services.POS;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Drinks;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Stores;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace CafeChain.Tests.POS
{
    public class POSWorkShiftCloseExceptionIssue85Tests
    {
        [Fact]
        public async Task CloseShiftByExceptionAsync_MissingReason_IsRejectedBeforeMutatingShift()
        {
            var repository = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            var supervisorAuth = new Mock<ISupervisorAuthService>(MockBehavior.Strict);
            var service = CreateWorkShiftService(repository, supervisorAuth);

            var result = await service.CloseShiftByExceptionAsync(17, 3, 85, new CloseShiftExceptionRequestDto
            {
                ActualEndingCash = 500000m,
                SupervisorPin = "1234"
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("lý do đóng ca ngoại lệ", result.Message);
            repository.Verify(repo => repo.UpdateShiftAsync(It.IsAny<WorkShift>()), Times.Never);
            supervisorAuth.Verify(auth => auth.VerifySupervisorPinAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task CloseShiftByExceptionAsync_CashierPinRejected_KeepsShiftOpen()
        {
            var shift = CreateOpenShift();
            var repository = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            var supervisorAuth = new Mock<ISupervisorAuthService>(MockBehavior.Strict);
            repository.Setup(repo => repo.GetActiveShiftAsync(17, 3)).ReturnsAsync(shift);
            repository.Setup(repo => repo.HasOpenPosPaymentAsync(85, 3)).ReturnsAsync(false);
            supervisorAuth
                .Setup(auth => auth.VerifySupervisorPinAsync("1111", 3))
                .ReturnsAsync(ServiceResult<SupervisorPinAuthorizationDto>.Failure("Không tìm thấy Supervisor/manager nào có mã PIN tại cửa hàng này."));
            var service = CreateWorkShiftService(repository, supervisorAuth);

            var result = await service.CloseShiftByExceptionAsync(17, 3, 85, CreateExceptionRequest("1111"));

            Assert.False(result.IsSuccess);
            Assert.Equal("Open", shift.Status);
            Assert.False(shift.IsExceptionClosed);
            Assert.False(shift.RequiresReconciliation);
            repository.Verify(repo => repo.UpdateShiftAsync(It.IsAny<WorkShift>()), Times.Never);
        }

        [Fact]
        public async Task CloseShiftByExceptionAsync_WrongPin_IsRejected()
        {
            var shift = CreateOpenShift();
            var repository = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            var supervisorAuth = new Mock<ISupervisorAuthService>(MockBehavior.Strict);
            repository.Setup(repo => repo.GetActiveShiftAsync(17, 3)).ReturnsAsync(shift);
            repository.Setup(repo => repo.HasOpenPosPaymentAsync(85, 3)).ReturnsAsync(false);
            supervisorAuth
                .Setup(auth => auth.VerifySupervisorPinAsync("0000", 3))
                .ReturnsAsync(ServiceResult<SupervisorPinAuthorizationDto>.Failure("Mã PIN không đúng."));
            var service = CreateWorkShiftService(repository, supervisorAuth);

            var result = await service.CloseShiftByExceptionAsync(17, 3, 85, CreateExceptionRequest("0000"));

            Assert.False(result.IsSuccess);
            Assert.Contains("PIN", result.Message);
            Assert.Equal("Open", shift.Status);
            repository.Verify(repo => repo.UpdateShiftAsync(It.IsAny<WorkShift>()), Times.Never);
        }

        [Fact]
        public async Task CloseShiftByExceptionAsync_BackendAwaitingPaymentStillBlocksExceptionClose()
        {
            var shift = CreateOpenShift();
            var repository = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            var supervisorAuth = new Mock<ISupervisorAuthService>(MockBehavior.Strict);
            repository.Setup(repo => repo.GetActiveShiftAsync(17, 3)).ReturnsAsync(shift);
            repository.Setup(repo => repo.HasOpenPosPaymentAsync(85, 3)).ReturnsAsync(true);
            var service = CreateWorkShiftService(repository, supervisorAuth);

            var result = await service.CloseShiftByExceptionAsync(17, 3, 85, CreateExceptionRequest("1234"));

            Assert.False(result.IsSuccess);
            Assert.Contains("giao dịch thanh toán chưa hoàn tất", result.Message);
            Assert.Equal("Open", shift.Status);
            supervisorAuth.Verify(auth => auth.VerifySupervisorPinAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
            repository.Verify(repo => repo.UpdateShiftAsync(It.IsAny<WorkShift>()), Times.Never);
        }

        [Fact]
        public async Task CloseShiftByExceptionAsync_SupervisorPinAndReason_ClosesAndPersistsReconciliationFields()
        {
            var shift = CreateOpenShift();
            var repository = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
            var supervisorAuth = new Mock<ISupervisorAuthService>(MockBehavior.Strict);
            repository.Setup(repo => repo.GetActiveShiftAsync(17, 3)).ReturnsAsync(shift);
            repository.Setup(repo => repo.HasOpenPosPaymentAsync(85, 3)).ReturnsAsync(false);
            repository.Setup(repo => repo.GetTotalCashSalesAsync(85)).ReturnsAsync(0m);
            repository.Setup(repo => repo.UpdateShiftAsync(It.IsAny<WorkShift>())).Returns(Task.CompletedTask);
            supervisorAuth
                .Setup(auth => auth.VerifySupervisorPinAsync("1234", 3))
                .ReturnsAsync(ServiceResult<SupervisorPinAuthorizationDto>.Success(new SupervisorPinAuthorizationDto
                {
                    SupervisorStaffId = 22,
                    SupervisorName = "Ca trưởng"
                }));
            var service = CreateWorkShiftService(repository, supervisorAuth);

            var result = await service.CloseShiftByExceptionAsync(17, 3, 85, CreateExceptionRequest("1234"));

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("Closed", shift.Status);
            Assert.True(shift.IsExceptionClosed);
            Assert.True(shift.RequiresReconciliation);
            Assert.Equal("Mất mạng kéo dài, còn đơn offline chưa sync.", shift.ExceptionCloseReason);
            Assert.Equal(22, shift.ExceptionClosedByStaffId);
            Assert.NotNull(shift.ExceptionClosedAt);
            Assert.NotNull(shift.EndTime);
            Assert.Equal(2, shift.OfflineOrderCountAtClose);
            Assert.Equal(90000m, shift.OfflineEstimatedTotalAtClose);
            Assert.Equal(90000m, shift.OfflineCashTotalAtClose);
            Assert.Equal(500000m, shift.ActualEndingCash);
            repository.Verify(repo => repo.UpdateShiftAsync(shift), Times.Once);
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
            Mock<ISupervisorAuthService> supervisorAuth)
        {
            return new WorkShiftService(
                repository.Object,
                Mock.Of<IHrAttendanceService>(),
                Mock.Of<IPOSOrderRepository>(),
                supervisorAuth.Object,
                Mock.Of<IOtpChallengeRepository>(),
                Mock.Of<IOtpPayloadFingerprintService>(),
                Mock.Of<ILogger<WorkShiftService>>());
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

        private static CloseShiftExceptionRequestDto CreateExceptionRequest(string pin)
        {
            return new CloseShiftExceptionRequestDto
            {
                ActualEndingCash = 500000m,
                ExceptionReason = "Mất mạng kéo dài, còn đơn offline chưa sync.",
                SupervisorPin = pin,
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
                CashDiscrepancy = 0m
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
    }
}
