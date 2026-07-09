using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Admin.Vouchers;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Application.Services.PayOSIntegration;
using CafeChain.Application.Services.POS;
using CafeChain.Controllers.Api.v1;
using CafeChain.Data;
using CafeChain.Hubs;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Drinks;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
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
    public class POSVietQrPayOsCommitTests : IntegrationTestBase
    {
        [Fact]
        public async Task CommitOrderAsync_VietQrCreatesAwaitingPaymentIntentWithoutPrint()
        {
            var repository = new Mock<IPOSOrderRepository>(MockBehavior.Strict);
            var workShiftService = new Mock<IWorkShiftService>(MockBehavior.Strict);
            var voucherService = new Mock<IAdminVoucherService>(MockBehavior.Strict);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var payOsService = new Mock<IPayOSService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderService>>();

            var dto = CreateBankingCommitDto(Guid.NewGuid());
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
                    order.OrderId = 201;
                })
                .ReturnsAsync((Order order) => order);

            repository
                .Setup(repo => repo.CreatePaymentAsync(It.IsAny<Payment>()))
                .Callback<Payment>(payment => capturedPayment = payment)
                .Returns(Task.CompletedTask);

            repository.Setup(repo => repo.CommitTransactionAsync()).Returns(Task.CompletedTask);

            payOsService
                .Setup(service => service.CreatePaymentLinkAsync(201))
                .ReturnsAsync(new PayOSCreateLinkResult
                {
                    CheckoutUrl = "https://pay.example/201",
                    QrCode = "qr",
                    OrderCode = 201000000001
                });

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
            Assert.Equal(SystemConstants.OrderStatuses.AwaitingPayment, capturedOrder!.OrderStatusId);
            Assert.Equal(SystemConstants.PaymentStatuses.Unpaid, capturedOrder.PaymentStatusId);
            Assert.NotNull(capturedPayment);
            Assert.Equal(SystemConstants.PaymentStatuses.Unpaid, capturedPayment!.PaymentStatusId);
            Assert.Null(capturedPayment.PaidAt);

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
        public async Task CommitOrder_ControllerDoesNotDeductInventoryForPendingVietQr()
        {
            var orderService = new Mock<IPOSOrderService>(MockBehavior.Strict);
            var inventoryService = new Mock<IInventoryDeductionService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderController>>();
            var dto = CreateBankingCommitDto(Guid.NewGuid());

            orderService
                .Setup(service => service.CommitOrderAsync(dto, 17, 3))
                .ReturnsAsync(ServiceResult<object>.Success(new
                {
                    orderId = 201,
                    requiresPayment = true,
                    paymentMethodId = 2,
                    checkoutUrl = "https://pay.example/201"
                } as object));

            var controller = CreateOrderController(orderService, inventoryService, logger);

            var response = await controller.CommitOrder(dto);

            Assert.IsType<OkObjectResult>(response);
            inventoryService.Verify(
                service => service.DeductStockForCommittedOrderAsync(
                    It.IsAny<List<POSSoldItemDto>>(),
                    It.IsAny<int>(),
                    It.IsAny<int>()),
                Times.Never);
        }

        [Fact]
        public async Task ProcessAsync_ConfirmedWebhookTransitionsToPaidAndRunsSideEffectsOnce()
        {
            using var context = CreateDbContext();
            var orderId = await SeedVietQrOrderAsync(context);
            var inventoryService = new Mock<IInventoryDeductionService>(MockBehavior.Strict);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var processor = CreateProcessor(context, inventoryService, printDispatcher);

            inventoryService
                .Setup(service => service.DeductStockForCommittedOrderAsync(
                    It.Is<List<POSSoldItemDto>>(items =>
                        items.Count == 1 &&
                        items[0].DrinkId == 10 &&
                        items[0].SizeId == 2 &&
                        items[0].Quantity == 1),
                    3,
                    orderId))
                .ReturnsAsync(ServiceResult.Success());

            printDispatcher
                .Setup(dispatcher => dispatcher.DispatchPrintJobAsync(
                    It.Is<Order>(order => order.OrderId == orderId),
                    3,
                    It.IsAny<string>(),
                    45000m,
                    false))
                .ReturnsAsync(true);

            var result = await processor.ProcessAsync(CreatePayload(orderCodeText: "201000000001"));

            Assert.Equal("SUCCESS", result.Code);
            Assert.True(result.ConfirmedPayment);

            var order = await context.Orders.Include(o => o.Payments).SingleAsync(o => o.OrderId == orderId);
            Assert.Equal(SystemConstants.OrderStatuses.Completed, order.OrderStatusId);
            Assert.Equal(SystemConstants.PaymentStatuses.Paid, order.PaymentStatusId);
            Assert.Contains(order.Payments, payment =>
                payment.PaymentMethodId == 2 &&
                payment.PaymentStatusId == SystemConstants.PaymentStatuses.Paid &&
                payment.TransactionCode == "PAYOS-TXN-1" &&
                payment.PaidAt.HasValue);

            inventoryService.Verify(
                service => service.DeductStockForCommittedOrderAsync(
                    It.IsAny<List<POSSoldItemDto>>(),
                    3,
                    orderId),
                Times.Once);
            printDispatcher.Verify(
                dispatcher => dispatcher.DispatchPrintJobAsync(
                    It.IsAny<Order>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<decimal>(),
                    It.IsAny<bool>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcessAsync_DuplicateWebhookDoesNotDeductInventoryOrPrintAgain()
        {
            using var context = CreateDbContext();
            await SeedVietQrOrderAsync(
                context,
                orderStatusId: SystemConstants.OrderStatuses.Completed,
                paymentStatusId: SystemConstants.PaymentStatuses.Paid,
                paymentLineStatusId: SystemConstants.PaymentStatuses.Paid);

            var inventoryService = new Mock<IInventoryDeductionService>(MockBehavior.Strict);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var processor = CreateProcessor(context, inventoryService, printDispatcher);

            inventoryService
                .Setup(service => service.DeductStockForCommittedOrderAsync(
                    It.IsAny<List<POSSoldItemDto>>(),
                    3,
                    It.IsAny<int>()))
                .ReturnsAsync(ServiceResult.Success("Đơn hàng đã được trừ kho trước đó."));

            var result = await processor.ProcessAsync(CreatePayload(orderCodeText: "201000000001"));

            Assert.Equal("ALREADY_PAID", result.Code);
            Assert.False(result.ConfirmedPayment);
            inventoryService.Verify(
                service => service.DeductStockForCommittedOrderAsync(
                    It.IsAny<List<POSSoldItemDto>>(),
                    3,
                    It.IsAny<int>()),
                Times.Once);
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
        public async Task ProcessAsync_CanceledOrderIsNotResurrected()
        {
            using var context = CreateDbContext();
            var orderId = await SeedVietQrOrderAsync(
                context,
                orderStatusId: SystemConstants.OrderStatuses.Cancelled,
                paymentStatusId: SystemConstants.PaymentStatuses.Failed,
                paymentLineStatusId: SystemConstants.PaymentStatuses.Failed);

            var inventoryService = new Mock<IInventoryDeductionService>(MockBehavior.Strict);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var processor = CreateProcessor(context, inventoryService, printDispatcher);

            var result = await processor.ProcessAsync(CreatePayload(orderCodeText: "201000000001"));

            Assert.Equal("PAYMENT_NOT_PAYABLE", result.Code);
            Assert.False(result.ConfirmedPayment);

            var order = await context.Orders.Include(o => o.Payments).SingleAsync(o => o.OrderId == orderId);
            Assert.Equal(SystemConstants.OrderStatuses.Cancelled, order.OrderStatusId);
            Assert.Equal(SystemConstants.PaymentStatuses.Failed, order.PaymentStatusId);
            Assert.All(order.Payments, payment => Assert.Equal(SystemConstants.PaymentStatuses.Failed, payment.PaymentStatusId));

            inventoryService.Verify(
                service => service.DeductStockForCommittedOrderAsync(
                    It.IsAny<List<POSSoldItemDto>>(),
                    It.IsAny<int>(),
                    It.IsAny<int>()),
                Times.Never);
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
        public async Task ProcessAsync_PrintFailureDoesNotFailConfirmedPayment()
        {
            using var context = CreateDbContext();
            var orderId = await SeedVietQrOrderAsync(context);
            var inventoryService = new Mock<IInventoryDeductionService>(MockBehavior.Strict);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var processor = CreateProcessor(context, inventoryService, printDispatcher);

            inventoryService
                .Setup(service => service.DeductStockForCommittedOrderAsync(
                    It.IsAny<List<POSSoldItemDto>>(),
                    3,
                    orderId))
                .ReturnsAsync(ServiceResult.Success());

            printDispatcher
                .Setup(dispatcher => dispatcher.DispatchPrintJobAsync(
                    It.IsAny<Order>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<decimal>(),
                    It.IsAny<bool>()))
                .ThrowsAsync(new InvalidOperationException("Printer offline"));

            var result = await processor.ProcessAsync(CreatePayload(orderCodeText: "201000000001"));

            Assert.Equal("SUCCESS", result.Code);
            Assert.True(result.ConfirmedPayment);

            var order = await context.Orders.SingleAsync(o => o.OrderId == orderId);
            Assert.Equal(SystemConstants.PaymentStatuses.Paid, order.PaymentStatusId);

            inventoryService.Verify(
                service => service.DeductStockForCommittedOrderAsync(
                    It.IsAny<List<POSSoldItemDto>>(),
                    3,
                    orderId),
                Times.Once);
            printDispatcher.Verify(
                dispatcher => dispatcher.DispatchPrintJobAsync(
                    It.IsAny<Order>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<decimal>(),
                    It.IsAny<bool>()),
                Times.Once);
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

        private PayOSWebhookProcessor CreateProcessor(
            AppDbContext context,
            Mock<IInventoryDeductionService> inventoryService,
            Mock<IPrintDispatcher> printDispatcher)
        {
            var orderHub = CreateHubContext<OrderHub>();
            var paymentHub = CreateHubContext<PaymentHub>();
            var logger = new Mock<ILogger<PayOSWebhookProcessor>>();

            return new PayOSWebhookProcessor(
                context,
                orderHub.Object,
                paymentHub.Object,
                printDispatcher.Object,
                inventoryService.Object,
                logger.Object);
        }

        private static Mock<IHubContext<THub>> CreateHubContext<THub>()
            where THub : Hub
        {
            var hubContext = new Mock<IHubContext<THub>>(MockBehavior.Strict);
            var clients = new Mock<IHubClients>(MockBehavior.Strict);
            var clientProxy = new Mock<IClientProxy>(MockBehavior.Strict);

            hubContext.Setup(context => context.Clients).Returns(clients.Object);
            clients.Setup(client => client.Group(It.IsAny<string>())).Returns(clientProxy.Object);
            clientProxy
                .Setup(proxy => proxy.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            return hubContext;
        }

        private static POSOrderController CreateOrderController(
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

        private async Task<int> SeedVietQrOrderAsync(
            AppDbContext context,
            int orderStatusId = SystemConstants.OrderStatuses.AwaitingPayment,
            int paymentStatusId = SystemConstants.PaymentStatuses.Unpaid,
            int paymentLineStatusId = SystemConstants.PaymentStatuses.Unpaid)
        {
            var order = new Order
            {
                StoreId = 3,
                StaffId = 17,
                WorkShiftId = 42,
                OrderStatusId = orderStatusId,
                PaymentStatusId = paymentStatusId,
                OrderTypeId = 1,
                Source = "POS",
                PaymentReference = "201000000001",
                SubTotal = 45000m,
                Total = 45000m,
                CreatedAt = DateTime.Now,
                OrderDetails = new List<OrderDetail>
                {
                    new()
                    {
                        DrinkId = 10,
                        SizeId = 2,
                        DrinkName = "Americano",
                        SizeName = "M",
                        Quantity = 1,
                        Price = 45000m,
                        Note = "",
                        OrderToppings = new List<OrderTopping>()
                    }
                },
                Payments = new List<Payment>
                {
                    new()
                    {
                        PaymentMethodId = 2,
                        PaymentStatusId = paymentLineStatusId,
                        Amount = 45000m
                    }
                }
            };

            context.Orders.Add(order);
            await context.SaveChangesAsync();
            return order.OrderId;
        }

        private static PayOSWebhookPayload CreatePayload(string orderCodeText)
        {
            return new PayOSWebhookPayload
            {
                OrderCodeText = orderCodeText,
                Amount = 45000m,
                TransactionId = "PAYOS-TXN-1",
                Description = "CafeChain #201",
                Status = "00",
                RawBody = "{}"
            };
        }

        private static POSOrderCommitDto CreateBankingCommitDto(Guid clientOrderId)
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
                    new() { PaymentMethodId = 2, Amount = 45000m }
                },
                PaymentMethodId = 2,
                ReceivedAmount = 45000m,
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
    }
}
