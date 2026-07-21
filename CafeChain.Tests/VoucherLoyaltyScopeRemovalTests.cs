using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Admin.Vouchers;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Services.PayOSIntegration;
using CafeChain.Application.Services.POS;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Controllers;
using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Drinks;
using CafeChain.Models.Loyalties;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Stores;
using CafeChain.Models.Vouchers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CafeChain.Tests
{
    /// <summary>
    /// Soft-removal of voucher + loyalty (điểm thưởng) from active product scope.
    /// Historical schema retained; active flows reject FEATURE_NOT_AVAILABLE.
    /// </summary>
    public sealed class VoucherLoyaltyScopeRemovalTests : IntegrationTestBase
    {
        [Fact]
        public void Voucher_AdminNavigation_IsNotAvailable()
        {
            Assert.IsType<NotFoundObjectResult>(new AdminVoucherController().Index());
        }

        [Fact]
        public void Voucher_AdminMutationEndpoint_IsDisabled()
        {
            var controller = new AdminVoucherController();
            var create = Assert.IsType<ObjectResult>(controller.Create(null));
            Assert.Equal(410, create.StatusCode);
            var toggle = Assert.IsType<JsonResult>(controller.ToggleStatus(1));
            Assert.Equal(ProductScopeErrorCodes.FeatureNotAvailable, ReadProp(toggle.Value, "errorCode"));
        }

        [Fact]
        public void Loyalty_AdminNavigation_IsNotAvailable()
        {
            Assert.IsType<NotFoundObjectResult>(new AdminVoucherController().Index());
            Assert.IsType<ObjectResult>(new AdminVoucherController().UpdateMemberLevel());
        }

        [Fact]
        public void Loyalty_AdminMutationEndpoint_IsDisabled()
        {
            var result = Assert.IsType<ObjectResult>(new AdminVoucherController().UpdateMemberLevel());
            Assert.Equal(410, result.StatusCode);
        }

        [Fact]
        public void Navigation_NoVoucherOrLoyaltyMenuEntries()
        {
            Assert.IsType<NotFoundObjectResult>(new AdminVoucherController().Index());
            Assert.IsType<NotFoundObjectResult>(new AdminWheelController().Index());
        }

        [Fact]
        public void POS_Checkout_DoesNotRenderVoucherInput()
        {
            var view = ReadRepoFile("CafeChain/Areas/Admin/Views/AdminPOS/Index.cshtml");
            Assert.DoesNotContain("id=\"voucherCode\"", view);
            Assert.DoesNotContain("Nhập mã voucher", view);
            Assert.DoesNotContain("applyVoucher()", view);
        }

        [Fact]
        public void POS_Checkout_DoesNotRenderLoyaltyControls()
        {
            var view = ReadRepoFile("CafeChain/Areas/Admin/Views/AdminPOS/Index.cshtml");
            var js = ReadRepoFile("CafeChain/wwwroot/js/pos-app.js");
            Assert.DoesNotContain("Dùng điểm tích lũy", view);
            Assert.DoesNotContain("successLoyalty", view);
            Assert.DoesNotContain("Dùng điểm tích lũy?", js);
            Assert.DoesNotContain("pointsToUse * 1000", js);
        }

        [Fact]
        public async Task POS_OrderCommit_DoesNotApplyVoucher()
        {
            var harness = CreateHarness();
            var dto = CreateCashDto();
            dto.VoucherCode = "WELCOME10";
            var result = await harness.Service.CommitOrderAsync(dto, 17, 3);
            Assert.False(result.IsSuccess);
            Assert.Equal(ProductScopeErrorCodes.FeatureNotAvailable, result.ErrorCode);
            harness.Repository.Verify(r => r.CreateOrderAsync(It.IsAny<Order>()), Times.Never);
            harness.Repository.Verify(r => r.CreateOrderVoucherAsync(It.IsAny<OrderVoucher>()), Times.Never);
            harness.Repository.Verify(r => r.CreateVoucherUsageAsync(It.IsAny<VoucherUsage>()), Times.Never);
            harness.Voucher.Verify(v => v.ValidateVoucherAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>()), Times.Never);
        }

        [Fact]
        public async Task POS_OrderCommit_DoesNotRedeemPoints()
        {
            var harness = CreateHarness();
            var dto = CreateCashDto();
            dto.CustomerId = 9;
            dto.PointsUsed = 10;
            var result = await harness.Service.CommitOrderAsync(dto, 17, 3);
            Assert.False(result.IsSuccess);
            Assert.Equal(ProductScopeErrorCodes.FeatureNotAvailable, result.ErrorCode);
            harness.Repository.Verify(r => r.CreatePointTransactionAsync(It.IsAny<PointTransaction>()), Times.Never);
            harness.Repository.Verify(r => r.CreateOrderAsync(It.IsAny<Order>()), Times.Never);
        }

        [Fact]
        public async Task POS_OrderCommit_DoesNotEarnPoints()
        {
            Order? captured = null;
            var harness = CreateHarness(onOrder: o => captured = o);
            var dto = CreateCashDto();
            dto.CustomerId = 9;
            var customer = new Models.Customers.Customer
            {
                CustomerId = 9,
                CustomerCode = "KH9",
                FullName = "C",
                CurrentPoints = 5,
                MemberLevelId = 1,
                Active = true
            };
            harness.Repository.Setup(r => r.GetCustomerByIdAsync(9)).ReturnsAsync(customer);
            harness.Repository.Setup(r => r.UpdateCustomerAsync(It.IsAny<Models.Customers.Customer>())).Returns(Task.CompletedTask);

            var result = await harness.Service.CommitOrderAsync(dto, 17, 3);
            Assert.True(result.IsSuccess);
            Assert.Equal(5, customer.CurrentPoints);
            Assert.Equal(0, ReadProp(result.Data, "earnedPoints"));
            harness.Repository.Verify(r => r.CreatePointTransactionAsync(It.IsAny<PointTransaction>()), Times.Never);
            Assert.NotNull(captured);
            Assert.Equal(0, captured!.PointsUsed);
            Assert.Equal(0m, captured.VoucherDiscount);
            Assert.Equal(0m, captured.PointDiscount);
        }

        [Fact]
        public async Task POS_OrderTotal_EqualsServerPricedLinesWithoutVoucherOrPoints()
        {
            Order? captured = null;
            var harness = CreateHarness(onOrder: o => captured = o);
            var result = await harness.Service.CommitOrderAsync(CreateCashDto(), 17, 3);
            Assert.True(result.IsSuccess);
            Assert.NotNull(captured);
            Assert.Equal(45_000m, captured!.SubTotal);
            Assert.Equal(45_000m, captured.Total);
            Assert.Equal(0m, captured.VoucherDiscount);
            Assert.Equal(0m, captured.PointDiscount);
            Assert.Equal(0, captured.PointsUsed);
            Assert.Equal(45_000m, ReadProp(result.Data, "total"));
        }

        [Fact]
        public async Task POS_PayOSAmount_DoesNotIncludeVoucherOrPointAdjustment()
        {
            Order? captured = null;
            decimal? payOsAmount = null;
            var harness = CreateHarness(onOrder: o => captured = o);
            harness.PayOs
                .Setup(p => p.CreatePaymentLinkAsync(It.IsAny<int>()))
                .ReturnsAsync(new PayOSCreateLinkResult { CheckoutUrl = "u", QrCode = "q", OrderCode = 1 });
            harness.PayOs
                .Setup(p => p.CreatePaymentLinkAsync(It.IsAny<int>(), It.IsAny<decimal>()))
                .Callback<int, decimal>((_, amt) => payOsAmount = amt)
                .ReturnsAsync(new PayOSCreateLinkResult { CheckoutUrl = "u", QrCode = "q", OrderCode = 1 });

            var dto = CreateCashDto();
            dto.Payments = new List<PaymentLineDto> { new() { PaymentMethodId = 2, Amount = 45_000m } };
            dto.ReceivedAmount = 0;
            var result = await harness.Service.CommitOrderAsync(dto, 17, 3);
            Assert.True(result.IsSuccess);
            Assert.NotNull(captured);
            Assert.Equal(45_000m, captured!.Total);
            Assert.Equal(0m, captured.VoucherDiscount);
            // Full PayOS amount uses CreatePaymentLinkAsync(orderId) when amount == total
            harness.PayOs.Verify(p => p.CreatePaymentLinkAsync(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task POS_OfflineLegacyVoucherPayload_IsRejectedClearly()
        {
            var harness = CreateHarness(offline: true);
            var dto = CreateCashDto();
            dto.ClientOrderId = Guid.NewGuid();
            dto.VoucherCode = "LEGACY";
            var result = await harness.Service.CommitOfflineSyncedOrderAsync(dto, 17, 3, 42, DateTime.UtcNow);
            Assert.False(result.IsSuccess);
            Assert.Equal(ProductScopeErrorCodes.FeatureNotAvailable, result.ErrorCode);
            harness.Repository.Verify(r => r.CreateOrderAsync(It.IsAny<Order>()), Times.Never);
        }

        [Fact]
        public async Task POS_OfflineLegacyPointPayload_IsRejectedClearly()
        {
            var harness = CreateHarness(offline: true);
            var dto = CreateCashDto();
            dto.ClientOrderId = Guid.NewGuid();
            dto.PointsUsed = 3;
            var result = await harness.Service.CommitOfflineSyncedOrderAsync(dto, 17, 3, 42, DateTime.UtcNow);
            Assert.False(result.IsSuccess);
            Assert.Equal(ProductScopeErrorCodes.FeatureNotAvailable, result.ErrorCode);
        }

        [Fact]
        public async Task POS_OfflineSnapshot_DoesNotCreateVoucherOrLoyaltyEffects()
        {
            Order? captured = null;
            var harness = CreateHarness(offline: true, onOrder: o => captured = o);
            var dto = CreateCashDto();
            dto.ClientOrderId = Guid.NewGuid();
            var result = await harness.Service.CommitOfflineSyncedOrderAsync(dto, 17, 3, 42, DateTime.UtcNow);
            Assert.True(result.IsSuccess);
            Assert.NotNull(captured);
            Assert.Equal(0m, captured!.VoucherDiscount);
            Assert.Equal(0m, captured.PointDiscount);
            Assert.Equal(0, captured.PointsUsed);
            harness.Repository.Verify(r => r.CreateOrderVoucherAsync(It.IsAny<OrderVoucher>()), Times.Never);
            harness.Repository.Verify(r => r.CreateVoucherUsageAsync(It.IsAny<VoucherUsage>()), Times.Never);
            harness.Repository.Verify(r => r.CreatePointTransactionAsync(It.IsAny<PointTransaction>()), Times.Never);
        }

        [Fact]
        public async Task Voucher_NoNewUsageRecord_IsCreated()
        {
            var harness = CreateHarness();
            Assert.True((await harness.Service.CommitOrderAsync(CreateCashDto(), 17, 3)).IsSuccess);
            harness.Repository.Verify(r => r.CreateVoucherUsageAsync(It.IsAny<VoucherUsage>()), Times.Never);
            harness.Repository.Verify(r => r.CreateOrderVoucherAsync(It.IsAny<OrderVoucher>()), Times.Never);
        }

        [Fact]
        public async Task Loyalty_NoPointTransaction_IsCreatedAfterPayment()
        {
            var harness = CreateHarness();
            var customer = new Models.Customers.Customer
            {
                CustomerId = 9,
                CustomerCode = "KH9",
                FullName = "C",
                CurrentPoints = 0,
                MemberLevelId = 1,
                Active = true
            };
            harness.Repository.Setup(r => r.GetCustomerByIdAsync(9)).ReturnsAsync(customer);
            harness.Repository.Setup(r => r.UpdateCustomerAsync(It.IsAny<Models.Customers.Customer>())).Returns(Task.CompletedTask);
            var dto = CreateCashDto();
            dto.CustomerId = 9;
            Assert.True((await harness.Service.CommitOrderAsync(dto, 17, 3)).IsSuccess);
            harness.Repository.Verify(r => r.CreatePointTransactionAsync(It.IsAny<PointTransaction>()), Times.Never);
        }

        [Fact]
        public async Task POS_ValidateVoucherApi_IsDisabled()
        {
            var controller = new PosController();
            var action = await controller.ValidateVoucher(new PosController.VoucherValidationRequest
            {
                Code = "X",
                CustomerId = 1,
                SubTotal = 1000
            });
            var json = Assert.IsType<JsonResult>(action);
            Assert.Equal(false, ReadProp(json.Value, "success"));
            Assert.Equal(ProductScopeErrorCodes.FeatureNotAvailable, ReadProp(json.Value, "errorCode"));
        }

        [Fact]
        public void POS_SellingPrice_RemainsDrinkSizePlusToppings()
        {
            // Guard: line price composition still DrinkSize + toppings in POSOrderService source.
            var src = ReadRepoFile("CafeChain/Application/Services/POS/POSOrderService.cs");
            Assert.Contains("itemBasePrice + toppingTotal", src);
            Assert.DoesNotContain("voucherDiscount = voucher.DiscountAmount", src);
            Assert.DoesNotContain("actualPointsUsed = dto.PointsUsed", src);
        }

        [Fact]
        public void POS_ActualCogs_RemainsFIFOInventoryLayers()
        {
            var src = ReadRepoFile("CafeChain/Application/Services/Inventories/InventoryDeductionService.cs");
            Assert.Contains("SalesCostAllocation", src);
            Assert.Contains("InventoryCostLayer", src);
        }

        [Fact]
        public async Task POS_CashRefund_NewOrder_IsNotBlockedByVoucherOrLoyalty()
        {
            using var ctx = CreateDbContext();
            await SeedRefundOrderAsync(ctx, voucherDiscount: 0, pointsUsed: 0, pointDiscount: 0);
            var svc = new OrderRefundService(ctx, NullLogger<OrderRefundService>.Instance);
            var r = await svc.RequestFullRefundAsync(new RequestFullOrderRefundDto
            {
                OrderId = 9001,
                RefundKey = Guid.NewGuid(),
                Reason = "clean new order refund"
            }, staffId: 9002, 9001, new[] { RoleConstants.StoreManager, RoleConstants.BusinessOwner });
            Assert.True(r.IsSuccess, r.Message);
            Assert.NotEqual(OrderRefundFailureCodes.LoyaltyReversalNotSupported, r.ErrorCode);
        }

        [Fact]
        public async Task POS_Refund_HistoricalVoucherOrder_RemainsFailClosed()
        {
            using var ctx = CreateDbContext();
            await SeedRefundOrderAsync(ctx, voucherDiscount: 1000m, pointsUsed: 0, pointDiscount: 0);
            var svc = new OrderRefundService(ctx, NullLogger<OrderRefundService>.Instance);
            var r = await svc.RequestFullRefundAsync(new RequestFullOrderRefundDto
            {
                OrderId = 9001,
                RefundKey = Guid.NewGuid(),
                Reason = "historical voucher"
            }, 9002, 9001, new[] { RoleConstants.StoreManager });
            Assert.False(r.IsSuccess);
            Assert.Equal(OrderRefundFailureCodes.LoyaltyReversalNotSupported, r.ErrorCode);
        }

        [Fact]
        public async Task POS_Refund_HistoricalLoyaltyOrder_RemainsFailClosed()
        {
            using var ctx = CreateDbContext();
            await SeedRefundOrderAsync(ctx, voucherDiscount: 0, pointsUsed: 5, pointDiscount: 5000m);
            var svc = new OrderRefundService(ctx, NullLogger<OrderRefundService>.Instance);
            var r = await svc.RequestFullRefundAsync(new RequestFullOrderRefundDto
            {
                OrderId = 9001,
                RefundKey = Guid.NewGuid(),
                Reason = "historical loyalty"
            }, 9002, 9001, new[] { RoleConstants.StoreManager });
            Assert.False(r.IsSuccess);
            Assert.Equal(OrderRefundFailureCodes.LoyaltyReversalNotSupported, r.ErrorCode);
        }

        // ── helpers ──────────────────────────────────────────────

        private sealed class Harness
        {
            public required POSOrderService Service { get; init; }
            public required Mock<IPOSOrderRepository> Repository { get; init; }
            public required Mock<IAdminVoucherService> Voucher { get; init; }
            public required Mock<IPayOSService> PayOs { get; init; }
        }

        private static Harness CreateHarness(bool offline = false, Action<Order>? onOrder = null)
        {
            var repository = new Mock<IPOSOrderRepository>(MockBehavior.Strict);
            var workShift = new Mock<IWorkShiftService>(MockBehavior.Strict);
            var voucher = new Mock<IAdminVoucherService>(MockBehavior.Strict);
            var print = new Mock<IPrintDispatcher>(MockBehavior.Loose);
            var payOs = new Mock<IPayOSService>(MockBehavior.Strict);
            var logger = new Mock<ILogger<POSOrderService>>();

            var shift = new WorkShift
            {
                ShiftId = 42,
                StoreId = 3,
                UserId = 17,
                Status = "Open",
                StartingCash = 500_000m,
                ExpectedEndingCash = 500_000m
            };

            repository.Setup(r => r.FindOrderByClientOrderIdAsync(It.IsAny<Guid>())).ReturnsAsync((Order?)null);
            repository.Setup(r => r.BeginTransactionAsync()).Returns(Task.CompletedTask);
            repository.Setup(r => r.RollbackTransactionAsync()).Returns(Task.CompletedTask);
            repository.Setup(r => r.CommitTransactionAsync()).Returns(Task.CompletedTask);
            repository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
            repository.Setup(r => r.GetDrinkWithSizesAsync(10, 3)).ReturnsAsync(CreateDrink());
            repository.Setup(r => r.GetValidToppingsForOrderItemAsync(3, 10, It.IsAny<List<int>>()))
                .ReturnsAsync(new List<Topping>());
            repository.Setup(r => r.CreateOrderAsync(It.IsAny<Order>()))
                .Callback<Order>(o =>
                {
                    o.OrderId = 101;
                    onOrder?.Invoke(o);
                })
                .ReturnsAsync((Order o) => o);
            repository.Setup(r => r.CreatePaymentAsync(It.IsAny<Payment>())).Returns(Task.CompletedTask);

            if (offline)
            {
                workShift.Setup(s => s.GetShiftByIdAsync(42, 17, 3)).ReturnsAsync(shift);
            }
            else
            {
                workShift.Setup(s => s.GetActiveShiftAsync(17, 3)).ReturnsAsync(shift);
            }

            print.Setup(p => p.DispatchPrintJobAsync(It.IsAny<Order>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<bool>()))
                .ReturnsAsync(true);

            var service = new POSOrderService(
                repository.Object,
                workShift.Object,
                voucher.Object,
                print.Object,
                payOs.Object,
                logger.Object);

            return new Harness
            {
                Service = service,
                Repository = repository,
                Voucher = voucher,
                PayOs = payOs
            };
        }

        private static POSOrderCommitDto CreateCashDto() => new()
        {
            ClientOrderId = Guid.NewGuid(),
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
                new() { PaymentMethodId = 1, Amount = 45_000m }
            },
            OrderTypeId = 1,
            ReceivedAmount = 50_000m,
            PaymentMethodId = 1,
            VoucherCode = null,
            PointsUsed = 0
        };

        private static Drink CreateDrink()
        {
            var size = new Size { SizeId = 2, Name = "M" };
            return new Drink
            {
                DrinkId = 10,
                Name = "Tra sua",
                Active = true,
                DrinkSizes = new List<DrinkSize>
                {
                    new() { DrinkId = 10, SizeId = 2, Price = 45_000m, Active = true, Size = size }
                }
            };
        }

        private static async Task SeedRefundOrderAsync(
            AppDbContext ctx,
            decimal voucherDiscount,
            int pointsUsed,
            decimal pointDiscount)
        {
            const int storeId = 9001;
            const int staffId = 9002;
            const int orderId = 9001;

            if (!await ctx.Stores.AnyAsync(s => s.StoreId == storeId))
            {
                ctx.Stores.Add(new Store
                {
                    StoreId = storeId,
                    Name = "Refund Scope Store",
                    Address = "1 Test",
                    Phone = "0900000000",
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (!await ctx.Staffs.AnyAsync(s => s.StaffId == staffId))
            {
                ctx.Staffs.Add(new Models.Staffs.Staff
                {
                    StaffId = staffId,
                    StoreId = storeId,
                    FullName = "Mgr Scope",
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            var total = 10_000m - voucherDiscount - pointDiscount;
            ctx.Orders.Add(new Order
            {
                OrderId = orderId,
                StoreId = storeId,
                StaffId = staffId,
                OrderStatusId = SystemConstants.OrderStatuses.Completed,
                PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                OrderTypeId = SystemConstants.OrderTypes.DineIn,
                Source = "POS",
                SubTotal = 10_000m,
                Total = total,
                VoucherDiscount = voucherDiscount,
                PointDiscount = pointDiscount,
                PointsUsed = pointsUsed,
                CreatedAt = DateTime.UtcNow
            });
            ctx.Payments.Add(new Payment
            {
                OrderId = orderId,
                PaymentMethodId = 1,
                Amount = total,
                PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                PaidAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        private static object? ReadProp(object? obj, string name)
            => obj?.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
                ?.GetValue(obj);

        private static string ReadRepoFile(string relativePath)
        {
            var root = FindRepoRoot();
            return System.IO.File.ReadAllText(System.IO.Path.Combine(root, relativePath));
        }

        private static string FindRepoRoot()
        {
            var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CafeChain", "CafeChain.csproj"))
                    || System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CafeChain.slnx")))
                {
                    // Prefer parent that contains CafeChain/ project folder.
                    if (System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "CafeChain")))
                        return dir.FullName;
                }
                dir = dir.Parent;
            }
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        }
    }
}
