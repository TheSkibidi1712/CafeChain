using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Admin.Vouchers;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Services.PayOSIntegration;
using CafeChain.Application.Services.POS;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Drinks;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Stores;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace CafeChain.Tests.POS
{
    public class POSWorkShiftGuardTests
    {
        [Fact]
        public async Task CommitOrderAsync_WithoutOpenWorkShift_ReturnsFailureAndDoesNotCreateOrder()
        {
            var repository = new Mock<IPOSOrderRepository>(MockBehavior.Strict);
            var workShiftService = new Mock<IWorkShiftService>(MockBehavior.Strict);
            var voucherService = new Mock<IAdminVoucherService>(MockBehavior.Strict);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var payOsService = new Mock<IPayOSService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderService>>();

            var dto = CreateCashCommitDto();

            workShiftService
                .Setup(service => service.GetActiveShiftAsync(17, 3))
                .ReturnsAsync((WorkShift?)null);

            var service = new POSOrderService(
                repository.Object,
                workShiftService.Object,
                voucherService.Object,
                printDispatcher.Object,
                payOsService.Object,
                logger.Object);

            var result = await service.CommitOrderAsync(dto, userId: 17, storeId: 3);

            Assert.False(result.IsSuccess);
            Assert.Contains("Phiên két tiền", result.Message);

            repository.Verify(repo => repo.CreateOrderAsync(It.IsAny<Order>()), Times.Never);
            repository.Verify(repo => repo.BeginTransactionAsync(), Times.Never);
            printDispatcher.Verify(
                dispatcher => dispatcher.DispatchPrintJobAsync(
                    It.IsAny<Order>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<decimal>(),
                    It.IsAny<bool>()),
                Times.Never);
        }

        [Fact]
        public async Task CommitOrderAsync_WithOpenWorkShift_CreatesOrderLinkedToWorkShiftStaffAndStore()
        {
            var repository = new Mock<IPOSOrderRepository>(MockBehavior.Strict);
            var workShiftService = new Mock<IWorkShiftService>(MockBehavior.Strict);
            var voucherService = new Mock<IAdminVoucherService>(MockBehavior.Strict);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var payOsService = new Mock<IPayOSService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderService>>();

            var dto = CreateCashCommitDto(skipPrint: true);
            Order? capturedOrder = null;
            var activeWorkShift = new WorkShift
            {
                ShiftId = 42,
                StoreId = 3,
                UserId = 17,
                Status = "Open",
                StartingCash = 500000m,
                ExpectedEndingCash = 500000m
            };

            workShiftService
                .Setup(service => service.GetActiveShiftAsync(17, 3))
                .ReturnsAsync(activeWorkShift);

            repository
                .Setup(repo => repo.BeginTransactionAsync())
                .Returns(Task.CompletedTask);

            repository
                .Setup(repo => repo.GetDrinkWithSizesAsync(10, 3))
                .ReturnsAsync(new Drink
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
                });

            repository
                .Setup(repo => repo.CreateOrderAsync(It.IsAny<Order>()))
                .Callback<Order>(order =>
                {
                    capturedOrder = order;
                    order.OrderId = 101;
                })
                .ReturnsAsync((Order order) => order);

            repository
                .Setup(repo => repo.CreatePaymentAsync(It.IsAny<Payment>()))
                .Returns(Task.CompletedTask);

            repository
                .Setup(repo => repo.SaveChangesAsync())
                .Returns(Task.CompletedTask);

            repository
                .Setup(repo => repo.CommitTransactionAsync())
                .Returns(Task.CompletedTask);

            var service = new POSOrderService(
                repository.Object,
                workShiftService.Object,
                voucherService.Object,
                printDispatcher.Object,
                payOsService.Object,
                logger.Object);

            var result = await service.CommitOrderAsync(dto, userId: 17, storeId: 3);

            Assert.True(result.IsSuccess);
            Assert.NotNull(capturedOrder);
            Assert.Equal(42, capturedOrder!.WorkShiftId);
            Assert.Equal(17, capturedOrder.StaffId);
            Assert.Equal(3, capturedOrder.StoreId);

            repository.Verify(repo => repo.CreateOrderAsync(It.IsAny<Order>()), Times.Once);
            repository.Verify(repo => repo.CommitTransactionAsync(), Times.Once);
            printDispatcher.Verify(
                dispatcher => dispatcher.DispatchPrintJobAsync(
                    It.IsAny<Order>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<decimal>(),
                    It.IsAny<bool>()),
                Times.Never);
        }

        [Fact]
        public async Task CommitOrderAsync_WithSessionBoundWorkShift_UsesExactBoundShift()
        {
            var repository = new Mock<IPOSOrderRepository>(MockBehavior.Strict);
            var workShiftService = new Mock<IWorkShiftService>(MockBehavior.Strict);
            var voucherService = new Mock<IAdminVoucherService>(MockBehavior.Strict);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var payOsService = new Mock<IPayOSService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderService>>();
            var dto = CreateCashCommitDto(skipPrint: true);
            dto.BoundWorkShiftId = 84;

            var boundShift = new WorkShift
            {
                ShiftId = 84,
                StoreId = 3,
                UserId = 17,
                Status = "Open",
                StartingCash = 500000m,
                ExpectedEndingCash = 500000m
            };

            workShiftService
                .Setup(service => service.GetShiftByIdAsync(84, 17, 3))
                .ReturnsAsync(boundShift);
            repository.Setup(repo => repo.BeginTransactionAsync()).Returns(Task.CompletedTask);
            repository.Setup(repo => repo.GetDrinkWithSizesAsync(10, 3)).ReturnsAsync(new Drink
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
                        Size = new Size { SizeId = 2, Name = "M", Active = true }
                    }
                }
            });
            repository.Setup(repo => repo.CreateOrderAsync(It.IsAny<Order>()))
                .Callback<Order>(order => order.OrderId = 102)
                .ReturnsAsync((Order order) => order);
            repository.Setup(repo => repo.CreatePaymentAsync(It.IsAny<Payment>())).Returns(Task.CompletedTask);
            repository.Setup(repo => repo.SaveChangesAsync()).Returns(Task.CompletedTask);
            repository.Setup(repo => repo.CommitTransactionAsync()).Returns(Task.CompletedTask);

            var service = new POSOrderService(
                repository.Object,
                workShiftService.Object,
                voucherService.Object,
                printDispatcher.Object,
                payOsService.Object,
                logger.Object);

            var result = await service.CommitOrderAsync(dto, userId: 17, storeId: 3);

            Assert.True(result.IsSuccess);
            workShiftService.Verify(service => service.GetShiftByIdAsync(84, 17, 3), Times.Exactly(2));
            workShiftService.Verify(
                service => service.GetActiveShiftAsync(It.IsAny<int>(), It.IsAny<int>()),
                Times.Never);
        }

        [Fact]
        public void BoundWorkShiftId_CannotBeSuppliedByClientJson()
        {
            var dto = JsonSerializer.Deserialize<POSOrderCommitDto>("{\"boundWorkShiftId\":999}");

            Assert.NotNull(dto);
            Assert.Null(dto!.BoundWorkShiftId);
        }

        private static POSOrderCommitDto CreateCashCommitDto(bool skipPrint = false)
        {
            return new POSOrderCommitDto
            {
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
                SkipPrint = skipPrint
            };
        }
    }
}
