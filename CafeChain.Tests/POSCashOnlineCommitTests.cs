using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Admin.Vouchers;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Application.Services.PayOSIntegration;
using CafeChain.Application.Services.POS;
using CafeChain.Controllers.Api.v1;
using CafeChain.Hubs;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Drinks;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CafeChain.Tests.POS
{
    public class POSCashOnlineCommitTests
    {
        [Fact]
        public async Task CommitOrderAsync_CashOnline_CreatesPaidCompletedOrderAndPrintFailureDoesNotFailPayment()
        {
            var repository = new Mock<IPOSOrderRepository>(MockBehavior.Strict);
            var workShiftService = new Mock<IWorkShiftService>(MockBehavior.Strict);
            var voucherService = new Mock<IAdminVoucherService>(MockBehavior.Strict);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var payOsService = new Mock<IPayOSService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderService>>();

            var dto = CreateCashCommitDto(Guid.NewGuid());
            Order? capturedOrder = null;
            Payment? capturedPayment = null;
            var activeWorkShift = new WorkShift
            {
                ShiftId = 42,
                StoreId = 3,
                UserId = 17,
                Status = "Open",
                StartingCash = 500000m,
                ExpectedEndingCash = 500000m
            };

            repository
                .Setup(repo => repo.FindOrderByClientOrderIdAsync(dto.ClientOrderId!.Value))
                .ReturnsAsync((Order?)null);

            workShiftService
                .Setup(service => service.GetActiveShiftAsync(17, 3))
                .ReturnsAsync(activeWorkShift);

            repository.Setup(repo => repo.BeginTransactionAsync()).Returns(Task.CompletedTask);
            repository
                .Setup(repo => repo.GetDrinkWithSizesAsync(10, 3))
                .ReturnsAsync(CreateDrink());

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
                .Callback<Payment>(payment => capturedPayment = payment)
                .Returns(Task.CompletedTask);

            repository.Setup(repo => repo.SaveChangesAsync()).Returns(Task.CompletedTask);
            repository.Setup(repo => repo.CommitTransactionAsync()).Returns(Task.CompletedTask);

            printDispatcher
                .Setup(dispatcher => dispatcher.DispatchPrintJobAsync(
                    It.IsAny<Order>(),
                    3,
                    It.IsAny<string>(),
                    50000m,
                    true))
                .ReturnsAsync(false);

            var service = CreateOrderService(
                repository,
                workShiftService,
                voucherService,
                printDispatcher,
                payOsService,
                logger);

            var result = await service.CommitOrderAsync(dto, userId: 17, storeId: 3);

            Assert.True(result.IsSuccess);
            Assert.Equal(5000m, (decimal)result.Data!.GetType().GetProperty("changeAmount")!.GetValue(result.Data)!);
            Assert.NotNull(capturedOrder);
            Assert.Equal(SystemConstants.OrderStatuses.Completed, capturedOrder!.OrderStatusId);
            Assert.Equal(SystemConstants.PaymentStatuses.Paid, capturedOrder.PaymentStatusId);
            Assert.Equal(42, capturedOrder.WorkShiftId);
            Assert.NotNull(capturedPayment);
            Assert.Equal(SystemConstants.PaymentStatuses.Paid, capturedPayment!.PaymentStatusId);
            Assert.Equal(45000m, capturedPayment.Amount);
            Assert.Equal(50000m, capturedPayment.ReceivedAmount);
            Assert.Equal(5000m, capturedPayment.ChangeAmount);
            Assert.Equal(545000m, activeWorkShift.ExpectedEndingCash);

            repository.Verify(repo => repo.CreateOrderAsync(It.IsAny<Order>()), Times.Once);
            repository.Verify(repo => repo.CreatePaymentAsync(It.IsAny<Payment>()), Times.Once);
            printDispatcher.Verify(
                dispatcher => dispatcher.DispatchPrintJobAsync(
                    It.IsAny<Order>(),
                    3,
                    It.IsAny<string>(),
                    50000m,
                    true),
                Times.Once);
        }

        [Fact]
        public async Task CommitOrderAsync_CashOnline_ReceivedAmountEqualTotalStoresZeroChange()
        {
            var repository = new Mock<IPOSOrderRepository>(MockBehavior.Strict);
            var workShiftService = new Mock<IWorkShiftService>(MockBehavior.Strict);
            var voucherService = new Mock<IAdminVoucherService>(MockBehavior.Strict);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var payOsService = new Mock<IPayOSService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderService>>();

            var dto = CreateCashCommitDto(Guid.NewGuid());
            dto.ReceivedAmount = 45000m;
            dto.SkipPrint = true;

            Payment? capturedPayment = null;
            var activeWorkShift = new WorkShift
            {
                ShiftId = 42,
                StoreId = 3,
                UserId = 17,
                Status = "Open",
                StartingCash = 500000m,
                ExpectedEndingCash = 500000m
            };

            repository
                .Setup(repo => repo.FindOrderByClientOrderIdAsync(dto.ClientOrderId!.Value))
                .ReturnsAsync((Order?)null);

            workShiftService
                .Setup(service => service.GetActiveShiftAsync(17, 3))
                .ReturnsAsync(activeWorkShift);

            repository.Setup(repo => repo.BeginTransactionAsync()).Returns(Task.CompletedTask);
            repository
                .Setup(repo => repo.GetDrinkWithSizesAsync(10, 3))
                .ReturnsAsync(CreateDrink());
            repository
                .Setup(repo => repo.CreateOrderAsync(It.IsAny<Order>()))
                .Callback<Order>(order => order.OrderId = 102)
                .ReturnsAsync((Order order) => order);
            repository
                .Setup(repo => repo.CreatePaymentAsync(It.IsAny<Payment>()))
                .Callback<Payment>(payment => capturedPayment = payment)
                .Returns(Task.CompletedTask);
            repository.Setup(repo => repo.SaveChangesAsync()).Returns(Task.CompletedTask);
            repository.Setup(repo => repo.CommitTransactionAsync()).Returns(Task.CompletedTask);

            var service = CreateOrderService(
                repository,
                workShiftService,
                voucherService,
                printDispatcher,
                payOsService,
                logger);

            var result = await service.CommitOrderAsync(dto, userId: 17, storeId: 3);

            Assert.True(result.IsSuccess);
            Assert.Equal(0m, (decimal)result.Data!.GetType().GetProperty("changeAmount")!.GetValue(result.Data)!);
            Assert.NotNull(capturedPayment);
            Assert.Equal(45000m, capturedPayment!.Amount);
            Assert.Equal(45000m, capturedPayment.ReceivedAmount);
            Assert.Equal(0m, capturedPayment.ChangeAmount);
            Assert.Equal(545000m, activeWorkShift.ExpectedEndingCash);
        }

        [Fact]
        public async Task CommitOrderAsync_CashOnline_ReceivedAmountLessThanTotalIsRejected()
        {
            var repository = new Mock<IPOSOrderRepository>(MockBehavior.Strict);
            var workShiftService = new Mock<IWorkShiftService>(MockBehavior.Strict);
            var voucherService = new Mock<IAdminVoucherService>(MockBehavior.Strict);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var payOsService = new Mock<IPayOSService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderService>>();

            var dto = CreateCashCommitDto(Guid.NewGuid());
            dto.ReceivedAmount = 40000m;
            var activeWorkShift = new WorkShift
            {
                ShiftId = 42,
                StoreId = 3,
                UserId = 17,
                Status = "Open",
                StartingCash = 500000m,
                ExpectedEndingCash = 500000m
            };

            repository
                .Setup(repo => repo.FindOrderByClientOrderIdAsync(dto.ClientOrderId!.Value))
                .ReturnsAsync((Order?)null);
            workShiftService
                .Setup(service => service.GetActiveShiftAsync(17, 3))
                .ReturnsAsync(activeWorkShift);
            repository.Setup(repo => repo.BeginTransactionAsync()).Returns(Task.CompletedTask);
            repository
                .Setup(repo => repo.GetDrinkWithSizesAsync(10, 3))
                .ReturnsAsync(CreateDrink());
            repository.Setup(repo => repo.RollbackTransactionAsync()).Returns(Task.CompletedTask);

            var service = CreateOrderService(
                repository,
                workShiftService,
                voucherService,
                printDispatcher,
                payOsService,
                logger);

            var result = await service.CommitOrderAsync(dto, userId: 17, storeId: 3);

            Assert.False(result.IsSuccess);
            Assert.Contains("Tiền khách đưa", result.Message);
            Assert.Equal(500000m, activeWorkShift.ExpectedEndingCash);
            repository.Verify(repo => repo.CreateOrderAsync(It.IsAny<Order>()), Times.Never);
            repository.Verify(repo => repo.CreatePaymentAsync(It.IsAny<Payment>()), Times.Never);
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
        public async Task CommitOrderAsync_IdempotentRetry_ReturnsExistingOrderWithoutAutomaticPrint()
        {
            var repository = new Mock<IPOSOrderRepository>(MockBehavior.Strict);
            var workShiftService = new Mock<IWorkShiftService>(MockBehavior.Strict);
            var voucherService = new Mock<IAdminVoucherService>(MockBehavior.Strict);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var payOsService = new Mock<IPayOSService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderService>>();

            var clientOrderId = Guid.NewGuid();
            var dto = CreateCashCommitDto(clientOrderId);
            var existingOrder = new Order
            {
                OrderId = 88,
                ClientOrderId = clientOrderId,
                SubTotal = 45000m,
                Total = 45000m,
                PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                Payments = new List<Payment>
                {
                    new() { PaymentMethodId = 1, PaymentStatusId = SystemConstants.PaymentStatuses.Paid }
                }
            };

            repository
                .Setup(repo => repo.FindOrderByClientOrderIdAsync(clientOrderId))
                .ReturnsAsync(existingOrder);

            var service = CreateOrderService(
                repository,
                workShiftService,
                voucherService,
                printDispatcher,
                payOsService,
                logger);

            var result = await service.CommitOrderAsync(dto, userId: 17, storeId: 3);

            Assert.True(result.IsSuccess);
            Assert.Equal(true, result.Data!.GetType().GetProperty("isIdempotent")?.GetValue(result.Data));

            repository.Verify(repo => repo.BeginTransactionAsync(), Times.Never);
            repository.Verify(repo => repo.CreateOrderAsync(It.IsAny<Order>()), Times.Never);
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
        public async Task CommitOrder_ControllerRunsInventoryDeductionOnceForNewCashOrder()
        {
            var orderService = new Mock<IPOSOrderService>(MockBehavior.Strict);
            var inventoryService = new Mock<IInventoryDeductionService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderController>>();
            var dto = CreateCashCommitDto(Guid.NewGuid());

            orderService
                .Setup(service => service.CommitOrderAsync(dto, 17, 3))
                .ReturnsAsync(ServiceResult<object>.Success(new
                {
                    orderId = 101,
                    isIdempotent = false
                } as object));

            inventoryService
                .Setup(service => service.DeductStockForCommittedOrderAsync(
                    It.Is<List<POSSoldItemDto>>(items =>
                        items.Count == 1 &&
                        items[0].DrinkId == 10 &&
                        items[0].SizeId == 2 &&
                        items[0].Quantity == 1),
                    3,
                    101))
                .ReturnsAsync(ServiceResult.Success());

            var controller = CreateController(orderService, inventoryService, logger);

            var response = await controller.CommitOrder(dto);

            Assert.IsType<OkObjectResult>(response);
            inventoryService.Verify(
                service => service.DeductStockForCommittedOrderAsync(
                    It.IsAny<List<POSSoldItemDto>>(),
                    3,
                    101),
                Times.Once);
        }

        [Fact]
        public async Task CommitOrder_ControllerUsesCommittedOrderGuardForIdempotentRetryRepair()
        {
            var orderService = new Mock<IPOSOrderService>(MockBehavior.Strict);
            var inventoryService = new Mock<IInventoryDeductionService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderController>>();
            var dto = CreateCashCommitDto(Guid.NewGuid());

            orderService
                .Setup(service => service.CommitOrderAsync(dto, 17, 3))
                .ReturnsAsync(ServiceResult<object>.Success(new
                {
                    orderId = 101,
                    isIdempotent = true
                } as object));

            inventoryService
                .Setup(service => service.DeductStockForCommittedOrderAsync(
                    It.IsAny<List<POSSoldItemDto>>(),
                    3,
                    101))
                .ReturnsAsync(ServiceResult.Success("Đơn hàng đã được trừ kho trước đó."));

            var controller = CreateController(orderService, inventoryService, logger);

            var response = await controller.CommitOrder(dto);

            Assert.IsType<OkObjectResult>(response);
            inventoryService.Verify(
                service => service.DeductStockForCommittedOrderAsync(
                    It.IsAny<List<POSSoldItemDto>>(),
                    3,
                    101),
                Times.Once);
        }

        [Fact]
        public async Task DispatchPrintJobAsync_SendsReceiptAndDrinkLabelOnce()
        {
            var escPosBuilder = new Mock<IEscPosBuilder>(MockBehavior.Strict);
            var hubContext = new Mock<IHubContext<PrintBridgeHub>>(MockBehavior.Strict);
            var clients = new Mock<IHubClients>(MockBehavior.Strict);
            var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);
            var logger = new Mock<ILogger<PrintDispatcher>>();
            var sentJobs = new List<object>();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PrintBridge:EnableCupLabels"] = "true",
                    ["PrintBridge:CupLabelPrinterTarget"] = "Cashier"
                })
                .Build();

            var order = new Order
            {
                OrderId = 101,
                Store = new Store { StoreId = 3, Name = "CafeChain #1" },
                OrderDetails = new List<OrderDetail>
                {
                    new()
                    {
                        DrinkName = "Americano",
                        SizeName = "M",
                        Quantity = 1,
                        Price = 45000m,
                        OrderToppings = new List<OrderTopping>()
                    }
                }
            };

            escPosBuilder
                .Setup(builder => builder.BuildReceipt(order, "CafeChain #1", "Thu ngan", 50000m, true))
                .Returns(new byte[] { 1, 2, 3 });

            escPosBuilder
                .Setup(builder => builder.BuildCupLabels(order, "CafeChain #1", "Thu ngan"))
                .Returns(new byte[] { 4, 5, 6 });

            hubContext.Setup(context => context.Clients).Returns(clients.Object);
            clients.Setup(client => client.Group("PrintBridge_Store_3")).Returns(clientProxy.Object);
            clientProxy
                .Setup(proxy => proxy.SendCoreAsync(
                    "PrintJob",
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, object[], CancellationToken>((_, args, _) => sentJobs.Add(args.Single()))
                .Returns(Task.CompletedTask);

            var dispatcher = new PrintDispatcher(
                escPosBuilder.Object,
                hubContext.Object,
                configuration,
                logger.Object);

            var result = await dispatcher.DispatchPrintJobAsync(order, 3, "Thu ngan", 50000m, true);

            Assert.True(result);
            Assert.Equal(2, sentJobs.Count);
            Assert.Contains(sentJobs, job => HasJobType(job, "Receipt"));
            Assert.Contains(sentJobs, job => HasJobType(job, "DrinkLabel"));

            escPosBuilder.Verify(
                builder => builder.BuildReceipt(order, "CafeChain #1", "Thu ngan", 50000m, true),
                Times.Once);
            escPosBuilder.Verify(
                builder => builder.BuildCupLabels(order, "CafeChain #1", "Thu ngan"),
                Times.Once);
            clientProxy.Verify(
                proxy => proxy.SendCoreAsync("PrintJob", It.IsAny<object[]>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        private static POSOrderService CreateOrderService(
            Mock<IPOSOrderRepository> repository,
            Mock<IWorkShiftService> workShiftService,
            Mock<IAdminVoucherService> voucherService,
            Mock<IPrintDispatcher> printDispatcher,
            Mock<IPayOSService> payOsService,
            Mock<ILogger<POSOrderService>> logger)
        {
            return new POSOrderService(
                repository.Object,
                workShiftService.Object,
                voucherService.Object,
                printDispatcher.Object,
                payOsService.Object,
                logger.Object);
        }

        private static POSOrderController CreateController(
            Mock<IPOSOrderService> orderService,
            Mock<IInventoryDeductionService> inventoryService,
            Mock<ILogger<POSOrderController>> logger)
        {
            var controller = new POSOrderController(
                orderService.Object,
                inventoryService.Object,
                logger.Object);

            var identity = new ClaimsIdentity(new[]
            {
                new Claim("StaffId", "17"),
                new Claim("StoreId", "3")
            }, "TestAuth");

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };

            return controller;
        }

        private static POSOrderCommitDto CreateCashCommitDto(Guid clientOrderId)
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
                OrderTypeId = 1
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

        private static bool HasJobType(object job, string expectedJobType)
        {
            return job.GetType().GetProperty("jobType")?.GetValue(job)?.ToString() == expectedJobType;
        }
    }
}
