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
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Staffs;
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
    public class POSReprintIssue83Tests
    {
        [Fact]
        public async Task ReprintOrderAsync_PaidBackendOrder_DispatchesReceiptReprintOnce()
        {
            var order = CreatePaidPosOrder();
            var repository = CreateRepositoryWithOrder(order);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);

            printDispatcher
                .Setup(dispatcher => dispatcher.DispatchReceiptReprintAsync(order, 3, "Thu ngan", 100000m, true))
                .ReturnsAsync(true);

            var service = CreateOrderService(repository, printDispatcher);

            var result = await service.ReprintOrderAsync(
                order.OrderId,
                new POSOrderReprintRequestDto { Type = "receipt" },
                order.StoreId);

            Assert.True(result.IsSuccess);
            Assert.Equal("Đã gửi lệnh in lại hóa đơn.", result.Message);
            printDispatcher.Verify(
                dispatcher => dispatcher.DispatchReceiptReprintAsync(order, 3, "Thu ngan", 100000m, true),
                Times.Once);
            printDispatcher.Verify(
                dispatcher => dispatcher.DispatchDrinkLabelReprintAsync(It.IsAny<Order>(), It.IsAny<int>(), It.IsAny<string>()),
                Times.Never);
            VerifyNoDataSideEffects(repository);
        }

        [Fact]
        public async Task ReprintOrderAsync_PaidBackendOrder_DispatchesDrinkLabelReprintOnce()
        {
            var order = CreatePaidPosOrder();
            var repository = CreateRepositoryWithOrder(order);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);

            printDispatcher
                .Setup(dispatcher => dispatcher.DispatchDrinkLabelReprintAsync(order, 3, "Thu ngan"))
                .ReturnsAsync(true);

            var service = CreateOrderService(repository, printDispatcher);

            var result = await service.ReprintOrderAsync(
                order.OrderId,
                new POSOrderReprintRequestDto { Type = "drinkLabel" },
                order.StoreId);

            Assert.True(result.IsSuccess);
            Assert.Equal("Đã gửi lệnh in lại tem.", result.Message);
            printDispatcher.Verify(
                dispatcher => dispatcher.DispatchDrinkLabelReprintAsync(order, 3, "Thu ngan"),
                Times.Once);
            printDispatcher.Verify(
                dispatcher => dispatcher.DispatchReceiptReprintAsync(It.IsAny<Order>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<bool>()),
                Times.Never);
            VerifyNoDataSideEffects(repository);
        }

        [Fact]
        public async Task ReprintOrderAsync_UnpaidOrder_IsRejected()
        {
            var order = CreatePaidPosOrder(paymentStatusId: SystemConstants.PaymentStatuses.Unpaid);
            var repository = CreateRepositoryWithOrder(order);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var service = CreateOrderService(repository, printDispatcher);

            var result = await service.ReprintOrderAsync(
                order.OrderId,
                new POSOrderReprintRequestDto { Type = "receipt" },
                order.StoreId);

            Assert.False(result.IsSuccess);
            Assert.Equal("Chỉ có thể in lại đơn đã thanh toán.", result.Message);
            VerifyNoReprintDispatch(printDispatcher);
            VerifyNoDataSideEffects(repository);
        }

        [Fact]
        public async Task ReprintOrderAsync_CanceledOrder_IsRejected()
        {
            var order = CreatePaidPosOrder(orderStatusId: SystemConstants.OrderStatuses.Cancelled);
            var repository = CreateRepositoryWithOrder(order);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var service = CreateOrderService(repository, printDispatcher);

            var result = await service.ReprintOrderAsync(
                order.OrderId,
                new POSOrderReprintRequestDto { Type = "receipt" },
                order.StoreId);

            Assert.False(result.IsSuccess);
            Assert.Equal("Đơn đã hủy không thể in lại.", result.Message);
            VerifyNoReprintDispatch(printDispatcher);
            VerifyNoDataSideEffects(repository);
        }

        [Fact]
        public async Task ReprintOrderAsync_OrderFromAnotherStore_IsRejected()
        {
            var repository = new Mock<IPOSOrderRepository>(MockBehavior.Strict);
            repository
                .Setup(repo => repo.GetOrderForReprintAsync(83, 3))
                .ReturnsAsync((Order?)null);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var service = CreateOrderService(repository, printDispatcher);

            var result = await service.ReprintOrderAsync(
                83,
                new POSOrderReprintRequestDto { Type = "receipt" },
                3);

            Assert.False(result.IsSuccess);
            Assert.Equal("Không tìm thấy đơn hàng để in lại.", result.Message);
            VerifyNoReprintDispatch(printDispatcher);
            VerifyNoDataSideEffects(repository);
        }

        [Fact]
        public async Task ReprintOrder_Controller_DoesNotCallInventoryDeduction()
        {
            var orderService = new Mock<IPOSOrderService>(MockBehavior.Strict);
            var inventoryService = new Mock<IInventoryDeductionService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderController>>();

            orderService
                .Setup(service => service.ReprintOrderAsync(
                    83,
                    It.Is<POSOrderReprintRequestDto>(dto => dto.Type == "receipt"),
                    3))
                .ReturnsAsync(ServiceResult<object>.Success(
                    new { orderId = 83, type = "receipt" } as object,
                    "Đã gửi lệnh in lại hóa đơn."));

            var controller = CreateController(orderService, inventoryService, logger);

            var response = await controller.ReprintOrder(
                83,
                new POSOrderReprintRequestDto { Type = "receipt" });

            Assert.IsType<OkObjectResult>(response);
            inventoryService.Verify(
                service => service.DeductStockForCommittedOrderAsync(
                    It.IsAny<List<POSSoldItemDto>>(),
                    It.IsAny<int>(),
                    It.IsAny<int>()),
                Times.Never);
        }

        [Fact]
        public async Task DispatchReceiptReprintAsync_SendsReprintJobWithoutCashDrawerKick()
        {
            var escPosBuilder = new Mock<IEscPosBuilder>(MockBehavior.Strict);
            var hubContext = new Mock<IHubContext<PrintBridgeHub>>(MockBehavior.Strict);
            var clients = new Mock<IHubClients>(MockBehavior.Strict);
            var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);
            var logger = new Mock<ILogger<PrintDispatcher>>();
            var sentJobs = new List<object>();
            var configuration = CreatePrintConfiguration();
            var order = CreatePaidPosOrder();

            escPosBuilder
                .Setup(builder => builder.BuildReceipt(order, "CafeChain #1", "Thu ngan", 100000m, true, false))
                .Returns(new byte[] { 1, 2, 3 });

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

            var result = await dispatcher.DispatchReceiptReprintAsync(order, 3, "Thu ngan", 100000m, true);

            Assert.True(result);
            Assert.Single(sentJobs);
            Assert.True(HasJobType(sentJobs[0], "ReceiptReprint"));
            Assert.True(HasBoolProperty(sentJobs[0], "isReprint", true));
            Assert.True(HasBoolProperty(sentJobs[0], "isCashPayment", false));
            escPosBuilder.Verify(
                builder => builder.BuildReceipt(order, "CafeChain #1", "Thu ngan", 100000m, true, false),
                Times.Once);
        }

        [Fact]
        public async Task DispatchDrinkLabelReprintAsync_SendsSeparateDrinkLabelReprintJob()
        {
            var escPosBuilder = new Mock<IEscPosBuilder>(MockBehavior.Strict);
            var hubContext = new Mock<IHubContext<PrintBridgeHub>>(MockBehavior.Strict);
            var clients = new Mock<IHubClients>(MockBehavior.Strict);
            var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);
            var logger = new Mock<ILogger<PrintDispatcher>>();
            var sentJobs = new List<object>();
            var configuration = CreatePrintConfiguration();
            var order = CreatePaidPosOrder();

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

            var result = await dispatcher.DispatchDrinkLabelReprintAsync(order, 3, "Thu ngan");

            Assert.True(result);
            Assert.Single(sentJobs);
            Assert.True(HasJobType(sentJobs[0], "DrinkLabelReprint"));
            Assert.True(HasBoolProperty(sentJobs[0], "isReprint", true));
        }

        private static POSOrderService CreateOrderService(
            Mock<IPOSOrderRepository> repository,
            Mock<IPrintDispatcher> printDispatcher)
        {
            var workShiftService = new Mock<IWorkShiftService>(MockBehavior.Strict);
            var voucherService = new Mock<IAdminVoucherService>(MockBehavior.Strict);
            var payOsService = new Mock<IPayOSService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderService>>();

            return new POSOrderService(
                repository.Object,
                workShiftService.Object,
                voucherService.Object,
                printDispatcher.Object,
                payOsService.Object,
                logger.Object);
        }

        private static Mock<IPOSOrderRepository> CreateRepositoryWithOrder(Order order)
        {
            var repository = new Mock<IPOSOrderRepository>(MockBehavior.Strict);
            repository
                .Setup(repo => repo.GetOrderForReprintAsync(order.OrderId, order.StoreId))
                .ReturnsAsync(order);
            return repository;
        }

        private static Order CreatePaidPosOrder(
            int orderId = 83,
            int storeId = 3,
            int paymentStatusId = SystemConstants.PaymentStatuses.Paid,
            int orderStatusId = SystemConstants.OrderStatuses.Completed,
            string source = "POS")
        {
            return new Order
            {
                OrderId = orderId,
                StoreId = storeId,
                Source = source,
                OrderStatusId = orderStatusId,
                PaymentStatusId = paymentStatusId,
                OrderTypeId = SystemConstants.OrderTypes.DineIn,
                SubTotal = 100000m,
                Total = 100000m,
                CreatedAt = new DateTime(2026, 7, 7, 10, 30, 0),
                Store = new Store { StoreId = storeId, Name = "CafeChain #1" },
                Staff = new Staff { StaffId = 9, FullName = "Thu ngan" },
                Payments = new List<Payment>
                {
                    new()
                    {
                        OrderId = orderId,
                        Amount = 100000m,
                        PaymentMethodId = 1,
                        PaymentStatusId = paymentStatusId,
                        PaidAt = new DateTime(2026, 7, 7, 10, 31, 0)
                    }
                },
                OrderDetails = new List<OrderDetail>
                {
                    new()
                    {
                        OrderId = orderId,
                        DrinkId = 1,
                        DrinkName = "Ca phe sua da",
                        SizeName = "M",
                        Price = 50000m,
                        Quantity = 2,
                        Note = "",
                        OrderToppings = new List<OrderTopping>()
                    }
                }
            };
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
                new Claim("StaffId", "9"),
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

        private static IConfiguration CreatePrintConfiguration()
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PrintBridge:EnableCupLabels"] = "true",
                    ["PrintBridge:CupLabelPrinterTarget"] = "Cashier"
                })
                .Build();
        }

        private static void VerifyNoReprintDispatch(Mock<IPrintDispatcher> printDispatcher)
        {
            printDispatcher.Verify(
                dispatcher => dispatcher.DispatchReceiptReprintAsync(It.IsAny<Order>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<bool>()),
                Times.Never);
            printDispatcher.Verify(
                dispatcher => dispatcher.DispatchDrinkLabelReprintAsync(It.IsAny<Order>(), It.IsAny<int>(), It.IsAny<string>()),
                Times.Never);
        }

        private static void VerifyNoDataSideEffects(Mock<IPOSOrderRepository> repository)
        {
            repository.Verify(repo => repo.CreateOrderAsync(It.IsAny<Order>()), Times.Never);
            repository.Verify(repo => repo.CreatePaymentAsync(It.IsAny<Payment>()), Times.Never);
            repository.Verify(repo => repo.BeginTransactionAsync(), Times.Never);
            repository.Verify(repo => repo.CommitTransactionAsync(), Times.Never);
            repository.Verify(repo => repo.RollbackTransactionAsync(), Times.Never);
            repository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
        }

        private static bool HasJobType(object job, string jobType)
        {
            var property = job.GetType().GetProperty("jobType");
            return string.Equals(property?.GetValue(job)?.ToString(), jobType, StringComparison.Ordinal);
        }

        private static bool HasBoolProperty(object job, string propertyName, bool expectedValue)
        {
            var property = job.GetType().GetProperty(propertyName);
            return property?.GetValue(job) is bool value && value == expectedValue;
        }
    }
}
