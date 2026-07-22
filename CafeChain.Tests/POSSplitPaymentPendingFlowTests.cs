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
    public class POSSplitPaymentPendingFlowTests : IntegrationTestBase
    {
        [Fact]
        public async Task CommitOrderAsync_SplitCashVietQrCreatesAwaitingIntentWithoutOfficialSideEffects()
        {
            var repository = new Mock<IPOSOrderRepository>(MockBehavior.Strict);
            var workShiftService = new Mock<IWorkShiftService>(MockBehavior.Strict);
            var voucherService = new Mock<IAdminVoucherService>(MockBehavior.Strict);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var payOsService = new Mock<IPayOSService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderService>>();
            var dto = CreateSplitCommitDto(Guid.NewGuid());
            var capturedPayments = new List<Payment>();
            Order? capturedOrder = null;
            var activeWorkShift = CreateOpenShift();

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
                    order.OrderId = 301;
                })
                .ReturnsAsync((Order order) => order);
            repository
                .Setup(repo => repo.CreatePaymentAsync(It.IsAny<Payment>()))
                .Callback<Payment>(payment => capturedPayments.Add(payment))
                .Returns(Task.CompletedTask);
            repository.Setup(repo => repo.CommitTransactionAsync()).Returns(Task.CompletedTask);
            payOsService
                .Setup(service => service.CreatePaymentLinkAsync(301, 25000m))
                .ReturnsAsync(new PayOSCreateLinkResult
                {
                    CheckoutUrl = "https://pay.example/301",
                    QrCode = "qr-split",
                    OrderCode = 301000000001
                });

            var service = CreateOrderService(
                repository,
                workShiftService,
                voucherService,
                printDispatcher,
                payOsService,
                logger);

            var result = await service.CommitOrderAsync(dto, userId: 17, storeId: 3);

            Assert.True(result.IsSuccess);
            Assert.NotNull(capturedOrder);
            Assert.Equal(SystemConstants.OrderStatuses.AwaitingPayment, capturedOrder!.OrderStatusId);
            Assert.Equal(SystemConstants.PaymentStatuses.Unpaid, capturedOrder.PaymentStatusId);
            Assert.Equal(500000m, activeWorkShift.ExpectedEndingCash);
            Assert.Equal(2, capturedPayments.Count);
            Assert.All(capturedPayments, payment =>
            {
                Assert.Equal(SystemConstants.PaymentStatuses.Unpaid, payment.PaymentStatusId);
                Assert.Null(payment.PaidAt);
            });
            Assert.Contains(capturedPayments, payment => payment.PaymentMethodId == 1 && payment.Amount == 20000m);
            Assert.Contains(capturedPayments, payment => payment.PaymentMethodId == 2 && payment.Amount == 25000m);
            Assert.Equal(20000m, result.Data!.GetType().GetProperty("pendingCashAmount")?.GetValue(result.Data));
            Assert.Equal(25000m, result.Data.GetType().GetProperty("pendingVietQrAmount")?.GetValue(result.Data));

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
        public async Task CommitOrder_ControllerDoesNotDeductInventoryForPendingSplitVietQr()
        {
            var orderService = new Mock<IPOSOrderService>(MockBehavior.Strict);
            var inventoryService = new Mock<IInventoryDeductionService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderController>>();
            var dto = CreateSplitCommitDto(Guid.NewGuid());

            orderService
                .Setup(service => service.CommitOrderAsync(dto, 17, 3))
                .ReturnsAsync(ServiceResult<object>.Success(new
                {
                    orderId = 301,
                    requiresPayment = true,
                    paymentMethodId = 2,
                    checkoutUrl = "https://pay.example/301"
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
        public async Task ProcessAsync_ConfirmedSplitWebhookMarksBothPaymentsPaidAndRunsSideEffectsOnce()
        {
            using var context = CreateDbContext();
            var orderId = await SeedSplitOrderAsync(context);
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
                    20000m,
                    true))
                .ReturnsAsync(true);

            var result = await processor.ProcessAsync(CreatePayload(amount: 25000m));

            Assert.Equal("SUCCESS", result.Code);
            Assert.True(result.ConfirmedPayment);

            var order = await context.Orders.Include(o => o.Payments).SingleAsync(o => o.OrderId == orderId);
            var shift = await context.WorkShifts.SingleAsync(s => s.ShiftId == 42);
            Assert.Equal(SystemConstants.OrderStatuses.Completed, order.OrderStatusId);
            Assert.Equal(SystemConstants.PaymentStatuses.Paid, order.PaymentStatusId);
            Assert.All(order.Payments, payment => Assert.Equal(SystemConstants.PaymentStatuses.Paid, payment.PaymentStatusId));
            Assert.Contains(order.Payments, payment =>
                payment.PaymentMethodId == 2 &&
                payment.TransactionCode == "PAYOS-TXN-1" &&
                payment.PaidAt.HasValue);
            Assert.Contains(order.Payments, payment =>
                payment.PaymentMethodId == 1 &&
                payment.PaidAt.HasValue);
            Assert.Equal(520000m, shift.ExpectedEndingCash);
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
        public async Task ProcessAsync_DuplicateSplitWebhookDoesNotPostCashDeductInventoryOrPrintAgain()
        {
            using var context = CreateDbContext();
            await SeedSplitOrderAsync(
                context,
                orderStatusId: SystemConstants.OrderStatuses.Completed,
                paymentStatusId: SystemConstants.PaymentStatuses.Paid,
                paymentLineStatusId: SystemConstants.PaymentStatuses.Paid,
                expectedEndingCash: 520000m);
            var inventoryService = new Mock<IInventoryDeductionService>(MockBehavior.Strict);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var processor = CreateProcessor(context, inventoryService, printDispatcher);

            inventoryService
                .Setup(service => service.DeductStockForCommittedOrderAsync(
                    It.IsAny<List<POSSoldItemDto>>(),
                    3,
                    It.IsAny<int>()))
                .ReturnsAsync(ServiceResult.Success("Đơn hàng đã được trừ kho trước đó."));

            var result = await processor.ProcessAsync(CreatePayload(amount: 25000m));

            var shift = await context.WorkShifts.SingleAsync(s => s.ShiftId == 42);
            Assert.Equal("ALREADY_PAID", result.Code);
            Assert.False(result.ConfirmedPayment);
            Assert.Equal(520000m, shift.ExpectedEndingCash);
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
        public async Task CancelPayment_SplitIntentMarksUnpaidLinesFailedWithoutPostingCash()
        {
            using var context = CreateDbContext();
            var orderId = await SeedSplitOrderAsync(context);
            var logger = new Mock<ILogger<POSPaymentController>>();
            var controller = CreatePaymentController(context, logger);

            var response = await controller.CancelPayment(new CancelPaymentRequestDto
            {
                OrderId = orderId,
                Reason = "Khách đổi phương thức thanh toán",
                CashReturnedConfirmed = true,
                ReturnedAmount = 20000m,
                RequestKey = Guid.NewGuid().ToString("N")
            });

            Assert.IsType<OkObjectResult>(response);

            var order = await context.Orders.Include(o => o.Payments).SingleAsync(o => o.OrderId == orderId);
            var shift = await context.WorkShifts.SingleAsync(s => s.ShiftId == 42);
            Assert.Equal(SystemConstants.OrderStatuses.Cancelled, order.OrderStatusId);
            Assert.Equal(SystemConstants.PaymentStatuses.Failed, order.PaymentStatusId);
            Assert.All(order.Payments, payment => Assert.Equal(SystemConstants.PaymentStatuses.Failed, payment.PaymentStatusId));
            Assert.Equal(500000m, shift.ExpectedEndingCash);
            Assert.Contains(await context.TransactionLogs.ToListAsync(), log =>
                log.OrderId == orderId &&
                log.Status == "CASH_RETURNED" &&
                log.Amount == 20000m);
        }

        [Fact]
        public async Task CancelAfterCashReceived_RequiresReturnConfirmation()
        {
            using var context = CreateDbContext();
            var orderId = await SeedSplitOrderAsync(context);
            var controller = CreatePaymentController(context, new Mock<ILogger<POSPaymentController>>());

            var response = await controller.CancelPayment(new CancelPaymentRequestDto
            {
                OrderId = orderId,
                Reason = "Khách đổi phương thức thanh toán"
            });

            Assert.IsType<ConflictObjectResult>(response);
            var order = await context.Orders.AsNoTracking().SingleAsync(candidate => candidate.OrderId == orderId);
            var shift = await context.WorkShifts.AsNoTracking().SingleAsync(candidate => candidate.ShiftId == 42);
            Assert.Equal(SystemConstants.OrderStatuses.AwaitingPayment, order.OrderStatusId);
            Assert.Equal(SystemConstants.PaymentStatuses.Unpaid, order.PaymentStatusId);
            Assert.Equal(500000m, shift.ExpectedEndingCash);
            Assert.Empty(await context.TransactionLogs.ToListAsync());
        }

        [Fact]
        public async Task CancelTemporaryCash_AfterReturn_AuditsWithoutOrderOrDrawerPosting()
        {
            using var context = CreateDbContext();
            context.WorkShifts.Add(CreateOpenShift());
            await context.SaveChangesAsync();
            var controller = CreatePaymentController(context, new Mock<ILogger<POSPaymentController>>());
            var requestKey = Guid.NewGuid().ToString("N");

            var response = await controller.CancelTemporaryCash(new CancelTemporaryCashRequestDto
            {
                ClientOrderId = Guid.NewGuid(),
                PendingCashAmount = 20000m,
                ReturnedAmount = 20000m,
                CashReturnedConfirmed = true,
                Reason = "Khách hủy thanh toán",
                RequestKey = requestKey
            });

            Assert.IsType<OkObjectResult>(response);
            Assert.Empty(await context.Orders.ToListAsync());
            Assert.Empty(await context.Payments.ToListAsync());
            Assert.Equal(500000m, (await context.WorkShifts.AsNoTracking().SingleAsync()).ExpectedEndingCash);
            var audit = await context.RequestDeduplications.AsNoTracking().SingleAsync();
            Assert.Equal("POS_TEMPORARY_CASH_CANCEL", audit.ActionName);
            Assert.Equal("SUCCESS", audit.Status);
            Assert.Equal(requestKey, audit.RequestKey);
            Assert.Contains("20000", audit.RequestBody);
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

            AttachPosClaims(controller);
            return controller;
        }

        private static POSPaymentController CreatePaymentController(
            AppDbContext context,
            Mock<ILogger<POSPaymentController>> logger)
        {
            var controller = new POSPaymentController(context, logger.Object);
            AttachPosClaims(controller);
            return controller;
        }

        private static void AttachPosClaims(ControllerBase controller)
        {
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
        }

        private async Task<int> SeedSplitOrderAsync(
            AppDbContext context,
            int orderStatusId = SystemConstants.OrderStatuses.AwaitingPayment,
            int paymentStatusId = SystemConstants.PaymentStatuses.Unpaid,
            int paymentLineStatusId = SystemConstants.PaymentStatuses.Unpaid,
            decimal expectedEndingCash = 500000m)
        {
            context.WorkShifts.Add(CreateOpenShift(expectedEndingCash));

            var order = new Order
            {
                StoreId = 3,
                StaffId = 17,
                WorkShiftId = 42,
                OrderStatusId = orderStatusId,
                PaymentStatusId = paymentStatusId,
                OrderTypeId = 1,
                Source = "POS",
                PaymentReference = "301000000001",
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
                        PaymentMethodId = 1,
                        PaymentStatusId = paymentLineStatusId,
                        Amount = 20000m,
                        PaidAt = paymentLineStatusId == SystemConstants.PaymentStatuses.Paid ? DateTime.Now : null
                    },
                    new()
                    {
                        PaymentMethodId = 2,
                        PaymentStatusId = paymentLineStatusId,
                        Amount = 25000m,
                        PaidAt = paymentLineStatusId == SystemConstants.PaymentStatuses.Paid ? DateTime.Now : null
                    }
                }
            };

            context.Orders.Add(order);
            await context.SaveChangesAsync();
            return order.OrderId;
        }

        private static PayOSWebhookPayload CreatePayload(decimal amount)
        {
            return new PayOSWebhookPayload
            {
                OrderCodeText = "301000000001",
                Amount = amount,
                TransactionId = "PAYOS-TXN-1",
                Description = "CafeChain #301",
                Status = "00",
                RawBody = "{}"
            };
        }

        private static POSOrderCommitDto CreateSplitCommitDto(Guid clientOrderId)
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
                    new() { PaymentMethodId = 1, Amount = 20000m },
                    new() { PaymentMethodId = 2, Amount = 25000m }
                },
                PaymentMethodId = 2,
                ReceivedAmount = 20000m,
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

        private static WorkShift CreateOpenShift(decimal expectedEndingCash = 500000m)
        {
            return new WorkShift
            {
                ShiftId = 42,
                StoreId = 3,
                UserId = 17,
                Status = "Open",
                StartTime = DateTime.Now,
                StartingCash = 500000m,
                ExpectedEndingCash = expectedEndingCash
            };
        }
    }
}
