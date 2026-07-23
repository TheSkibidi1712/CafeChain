using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Admin.Vouchers;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Application.Services.PayOSIntegration;
using CafeChain.Application.Services.POS;
using CafeChain.Controllers.Api.v1;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Drinks;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;
using CafeChain.Tests.Testing;

namespace CafeChain.Tests.POS
{
    public class POSOfflineSyncIssue81Tests
    {
        [Fact]
        public async Task CommitOfflineSyncedOrderAsync_ClosedOriginalWorkShift_CreatesOrderLinkedToOriginalShiftWithoutActiveShiftOrPrint()
        {
            var repository = new Mock<IPOSOrderRepository>(MockBehavior.Strict);
            var workShiftService = new Mock<IWorkShiftService>(MockBehavior.Strict);
            var voucherService = new Mock<IAdminVoucherService>(MockBehavior.Strict);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var payOsService = new Mock<IPayOSService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderService>>();
            var clientOrderId = Guid.NewGuid();
            var soldAt = new DateTime(2026, 7, 1, 21, 30, 0);
            var dto = CreateOfflineCashCommitDto(clientOrderId);
            var closedShift = CreateShift(status: "Closed");
            Order? capturedOrder = null;
            Payment? capturedPayment = null;

            repository
                .Setup(repo => repo.FindOrderByClientOrderIdAsync(clientOrderId, It.IsAny<int>()))
                .ReturnsAsync((Order?)null);
            workShiftService
                .Setup(service => service.GetShiftByIdAsync(42))
                .ReturnsAsync(closedShift);
            repository.Setup(repo => repo.BeginTransactionAsync()).Returns(Task.CompletedTask);
            repository
                .Setup(repo => repo.GetDrinkWithSizesAsync(10, 3))
                .ReturnsAsync(CreateDrink());
            repository
                .Setup(repo => repo.CreateOrderAsync(It.IsAny<Order>()))
                .Callback<Order>(order =>
                {
                    capturedOrder = order;
                    order.OrderId = 501;
                })
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

            var result = await service.CommitOfflineSyncedOrderAsync(dto, 17, 3, 42, soldAt);

            Assert.True(result.IsSuccess);
            Assert.NotNull(capturedOrder);
            Assert.Equal(42, capturedOrder!.WorkShiftId);
            Assert.Equal(17, capturedOrder.StaffId);
            Assert.Equal(3, capturedOrder.StoreId);
            Assert.Equal(soldAt, capturedOrder.CreatedAt);
            Assert.Equal(clientOrderId, capturedOrder.ClientOrderId);
            Assert.Equal(SystemConstants.OrderStatuses.Completed, capturedOrder.OrderStatusId);
            Assert.Equal(SystemConstants.PaymentStatuses.Paid, capturedOrder.PaymentStatusId);
            Assert.NotNull(capturedPayment);
            Assert.Equal(SystemConstants.PaymentStatuses.Paid, capturedPayment!.PaymentStatusId);
            Assert.Equal(45000m, capturedPayment.Amount);
            Assert.Equal(50000m, capturedPayment.ReceivedAmount);
            Assert.Equal(5000m, capturedPayment.ChangeAmount);
            Assert.Equal(500000m, closedShift.ExpectedEndingCash);
            Assert.True(closedShift.RequiresReconciliation);
            Assert.True(closedShift.HasLateOfflineSync);
            Assert.Equal(1, closedShift.LateOfflineSyncCount);
            Assert.NotNull(closedShift.LastLateOfflineSyncedAt);
            Assert.Equal(false, result.Data!.GetType().GetProperty("isIdempotent")?.GetValue(result.Data));

            workShiftService.Verify(service => service.GetActiveShiftAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
            repository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
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
        public async Task CommitOfflineSyncedOrderAsync_DuplicateClientOrderId_ReturnsExistingOrderWithoutCreateOrPrint()
        {
            var repository = new Mock<IPOSOrderRepository>(MockBehavior.Strict);
            var workShiftService = new Mock<IWorkShiftService>(MockBehavior.Strict);
            var voucherService = new Mock<IAdminVoucherService>(MockBehavior.Strict);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var payOsService = new Mock<IPayOSService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderService>>();
            var clientOrderId = Guid.NewGuid();
            var dto = CreateOfflineCashCommitDto(clientOrderId);
            var existingOrder = new Order
            {
                OrderId = 502,
                ClientOrderId = clientOrderId,
                WorkShiftId = 42,
                StoreId = 3,
                SubTotal = 45000m,
                Total = 45000m
            };

            workShiftService
                .Setup(service => service.GetShiftByIdAsync(42))
                .ReturnsAsync(new WorkShift { ShiftId = 42, UserId = 17, StoreId = 3, Status = "Closed" });
            repository
                .Setup(repo => repo.FindOrderByClientOrderIdAsync(clientOrderId, It.IsAny<int>()))
                .ReturnsAsync(existingOrder);

            var service = CreateOrderService(
                repository,
                workShiftService,
                voucherService,
                printDispatcher,
                payOsService,
                logger);

            var result = await service.CommitOfflineSyncedOrderAsync(dto, 17, 3, 42, DateTime.Now);

            Assert.True(result.IsSuccess);
            Assert.Equal(true, result.Data!.GetType().GetProperty("isIdempotent")?.GetValue(result.Data));
            Assert.Equal(502, result.Data.GetType().GetProperty("orderId")?.GetValue(result.Data));

            repository.Verify(repo => repo.BeginTransactionAsync(), Times.Never);
            repository.Verify(repo => repo.CreateOrderAsync(It.IsAny<Order>()), Times.Never);
            workShiftService.Verify(service => service.GetShiftByIdAsync(42), Times.Once);
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
        public async Task CommitOfflineSyncedOrderAsync_MissingOrWrongWorkShift_FailsWithoutCreate()
        {
            var repository = new Mock<IPOSOrderRepository>(MockBehavior.Strict);
            var workShiftService = new Mock<IWorkShiftService>(MockBehavior.Strict);
            var voucherService = new Mock<IAdminVoucherService>(MockBehavior.Strict);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var payOsService = new Mock<IPayOSService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderService>>();
            var clientOrderId = Guid.NewGuid();
            var dto = CreateOfflineCashCommitDto(clientOrderId);

            repository
                .Setup(repo => repo.FindOrderByClientOrderIdAsync(clientOrderId, It.IsAny<int>()))
                .ReturnsAsync((Order?)null);
            workShiftService
                .Setup(service => service.GetShiftByIdAsync(42))
                .ReturnsAsync((WorkShift?)null);

            var service = CreateOrderService(
                repository,
                workShiftService,
                voucherService,
                printDispatcher,
                payOsService,
                logger);

            var result = await service.CommitOfflineSyncedOrderAsync(dto, 17, 3, 42, DateTime.Now);

            Assert.False(result.IsSuccess);
            Assert.Contains("WorkShift", result.Message);
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
        public async Task CommitOfflineSyncedOrderAsync_NonCashPayment_FailsWithoutCreate()
        {
            var repository = new Mock<IPOSOrderRepository>(MockBehavior.Strict);
            var workShiftService = new Mock<IWorkShiftService>(MockBehavior.Strict);
            var voucherService = new Mock<IAdminVoucherService>(MockBehavior.Strict);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var payOsService = new Mock<IPayOSService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderService>>();
            var dto = CreateOfflineCashCommitDto(Guid.NewGuid());
            dto.PaymentMethodId = 2;
            dto.Payments = new List<PaymentLineDto>
            {
                new() { PaymentMethodId = 2, Amount = 45000m }
            };

            var service = CreateOrderService(
                repository,
                workShiftService,
                voucherService,
                printDispatcher,
                payOsService,
                logger);

            var result = await service.CommitOfflineSyncedOrderAsync(dto, 17, 3, 42, DateTime.Now);

            Assert.False(result.IsSuccess);
            Assert.Contains("tiền mặt", result.Message);
            repository.Verify(repo => repo.CreateOrderAsync(It.IsAny<Order>()), Times.Never);
            repository.Verify(repo => repo.BeginTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task CommitOfflineSyncedOrderAsync_CashReceivedAmountLessThanTotalIsRejected()
        {
            var repository = new Mock<IPOSOrderRepository>(MockBehavior.Strict);
            var workShiftService = new Mock<IWorkShiftService>(MockBehavior.Strict);
            var voucherService = new Mock<IAdminVoucherService>(MockBehavior.Strict);
            var printDispatcher = new Mock<IPrintDispatcher>(MockBehavior.Strict);
            var payOsService = new Mock<IPayOSService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderService>>();
            var clientOrderId = Guid.NewGuid();
            var dto = CreateOfflineCashCommitDto(clientOrderId);
            dto.ReceivedAmount = 40000m;
            var openShift = CreateShift(status: "Open");

            repository
                .Setup(repo => repo.FindOrderByClientOrderIdAsync(clientOrderId, It.IsAny<int>()))
                .ReturnsAsync((Order?)null);
            workShiftService
                .Setup(service => service.GetShiftByIdAsync(42))
                .ReturnsAsync(openShift);
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

            var result = await service.CommitOfflineSyncedOrderAsync(dto, 17, 3, 42, DateTime.Now);

            Assert.False(result.IsSuccess);
            Assert.Contains("Tiền khách đưa", result.Message);
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
        public async Task SyncOfflineOrders_CreatedOrder_DeductsInventoryOnceAndUsesOriginalStaffStoreWorkShift()
        {
            var orderService = new Mock<IPOSOrderService>(MockBehavior.Strict);
            var inventoryService = new Mock<IInventoryDeductionService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderController>>();
            var offlineOrder = CreateOfflineOrderSyncDto();

            orderService
                .Setup(service => service.CommitOfflineSyncedOrderAsync(
                    It.Is<POSOrderCommitDto>(dto =>
                        dto.ClientOrderId == offlineOrder.ClientOrderId &&
                        dto.PaymentMethodId == 1 &&
                        dto.SkipPrint &&
                        dto.Items.Count == 1 &&
                        dto.Items[0].DrinkId == 10),
                    It.Is<OfflineOrderSyncContext>(context =>
                        context.ActorStaffId == 99 &&
                        context.ClaimedStaffId == 17 &&
                        context.ClaimedStoreId == 3 &&
                        context.WorkShiftId == 42 &&
                        context.SoldAt == offlineOrder.SoldAt!.Value)))
                .ReturnsAsync(ServiceResult<object>.Success(new
                {
                    orderId = 601,
                    storeId = 3,
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
                    601))
                .ReturnsAsync(ServiceResult.Success());

            var controller = CreateController(orderService, inventoryService, logger);

            var response = await controller.SyncOfflineOrders(new OfflineBatchSyncRequestDto
            {
                Orders = new List<OfflineOrderSyncDTO> { offlineOrder }
            });

            Assert.IsType<OkObjectResult>(response);
            orderService.Verify(
                service => service.CommitOfflineSyncedOrderAsync(
                    It.IsAny<POSOrderCommitDto>(),
                    It.Is<OfflineOrderSyncContext>(context =>
                        context.ActorStaffId == 99 &&
                        context.ClaimedStaffId == 17 &&
                        context.ClaimedStoreId == 3 &&
                        context.WorkShiftId == 42)),
                Times.Once);
            inventoryService.Verify(
                service => service.DeductStockForCommittedOrderAsync(
                    It.IsAny<List<POSSoldItemDto>>(),
                    3,
                    601),
                Times.Once);
        }

        [Fact]
        public async Task SyncOfflineOrders_DuplicateOrder_UsesCommittedOrderGuardWithoutCreatingDuplicate()
        {
            var orderService = new Mock<IPOSOrderService>(MockBehavior.Strict);
            var inventoryService = new Mock<IInventoryDeductionService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderController>>();
            var offlineOrder = CreateOfflineOrderSyncDto();

            orderService
                .Setup(service => service.CommitOfflineSyncedOrderAsync(
                    It.IsAny<POSOrderCommitDto>(),
                    It.Is<OfflineOrderSyncContext>(context =>
                        context.ActorStaffId == 99 &&
                        context.ClaimedStaffId == 17 &&
                        context.ClaimedStoreId == 3 &&
                        context.WorkShiftId == 42)))
                .ReturnsAsync(ServiceResult<object>.Success(new
                {
                    orderId = 601,
                    storeId = 3,
                    isIdempotent = true
                } as object));

            inventoryService
                .Setup(service => service.DeductStockForCommittedOrderAsync(
                    It.IsAny<List<POSSoldItemDto>>(),
                    3,
                    601))
                .ReturnsAsync(ServiceResult.Success("Đơn hàng đã được trừ kho trước đó."));

            var controller = CreateController(orderService, inventoryService, logger);

            var response = await controller.SyncOfflineOrders(new OfflineBatchSyncRequestDto
            {
                Orders = new List<OfflineOrderSyncDTO> { offlineOrder }
            });

            Assert.IsType<OkObjectResult>(response);
            inventoryService.Verify(
                service => service.DeductStockForCommittedOrderAsync(
                    It.IsAny<List<POSSoldItemDto>>(),
                    3,
                    601),
                Times.Once);
        }

        [Fact]
        public async Task SyncOfflineOrders_MissingWorkShiftId_FailsAndKeepsServiceAndInventoryUntouched()
        {
            var orderService = new Mock<IPOSOrderService>(MockBehavior.Strict);
            var inventoryService = new Mock<IInventoryDeductionService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderController>>();
            var offlineOrder = CreateOfflineOrderSyncDto();
            offlineOrder.WorkShiftId = null;
            var controller = CreateController(orderService, inventoryService, logger);

            var response = await controller.SyncOfflineOrders(new OfflineBatchSyncRequestDto
            {
                Orders = new List<OfflineOrderSyncDTO> { offlineOrder }
            });

            Assert.IsType<OkObjectResult>(response);
            orderService.Verify(
                service => service.CommitOfflineSyncedOrderAsync(
                    It.IsAny<POSOrderCommitDto>(),
                    It.IsAny<OfflineOrderSyncContext>()),
                Times.Never);
            inventoryService.Verify(
                service => service.DeductStockForCommittedOrderAsync(
                    It.IsAny<List<POSSoldItemDto>>(),
                    It.IsAny<int>(),
                    It.IsAny<int>()),
                Times.Never);
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
                logger.Object,
                null,
                null,
                AllowAllOrderAccessAuthorizationService.Instance);
        }

        private static POSOrderController CreateController(
            Mock<IPOSOrderService> orderService,
            Mock<IInventoryDeductionService> inventoryService,
            Mock<ILogger<POSOrderController>> logger)
        {
            var controller = new POSOrderController(
                orderService.Object,
                inventoryService.Object,
                logger.Object,
                AllowAllOrderAccessAuthorizationService.Instance);

            var identity = new ClaimsIdentity(new[]
            {
                new Claim("StaffId", "99"),
                new Claim("StoreId", "99"),
                new Claim(ClaimTypes.Role, RoleConstants.SalesStaff)
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

        private static OfflineOrderSyncDTO CreateOfflineOrderSyncDto()
        {
            return new OfflineOrderSyncDTO
            {
                ClientOrderId = Guid.NewGuid(),
                LocalId = "local-1",
                StoreId = 3,
                StaffId = 17,
                WorkShiftId = 42,
                SoldAt = new DateTime(2026, 7, 1, 21, 30, 0),
                PaymentMethodId = 1,
                TotalAmount = 45000m,
                ReceivedAmount = 50000m,
                ChangeAmount = 5000m,
                OrderTypeId = 1,
                Note = "",
                PaymentSnapshot = new OfflinePaymentSnapshotDTO
                {
                    PaymentMethodId = 1,
                    Amount = 45000m,
                    ReceivedAmount = 50000m,
                    ChangeAmount = 5000m
                },
                Details = new List<OfflineOrderDetailDTO>
                {
                    new()
                    {
                        ItemId = 10,
                        StoreMenuItemId = 100,
                        DrinkSizeId = 200,
                        ItemName = "Americano",
                        SizeId = 2,
                        Quantity = 1,
                        AcceptedBasePrice = 45000m,
                        UnitPrice = 45000m,
                        PriceSource = StoreMenuPriceSources.Global,
                        CatalogVersion = 1,
                        TotalPrice = 45000m,
                        Toppings = new List<POSOrderToppingDto>()
                    }
                },
                CartSnapshot = new List<OfflineCartSnapshotItemDTO>
                {
                    new()
                    {
                        MenuItemId = 10,
                        StoreMenuItemId = 100,
                        DrinkSizeId = 200,
                        Name = "Americano",
                        SizeId = 2,
                        Quantity = 1,
                        UnitPrice = 45000m,
                        EffectivePrice = 45000m,
                        PriceSource = StoreMenuPriceSources.Global,
                        CatalogVersion = 1,
                        Toppings = new List<OfflineCartSnapshotToppingDTO>()
                    }
                }
            };
        }

        private static WorkShift CreateShift(string status)
        {
            return new WorkShift
            {
                ShiftId = 42,
                StoreId = 3,
                UserId = 17,
                Status = status,
                StartingCash = 500000m,
                ExpectedEndingCash = 500000m
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
