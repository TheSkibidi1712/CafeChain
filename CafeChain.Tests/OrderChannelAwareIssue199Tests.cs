using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces;
using CafeChain.Application.Policies.Orders;
using CafeChain.Application.Services.Admin;
using CafeChain.Hubs;
using CafeChain.Infrastructure.Repositories.Admin.POS;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CafeChain.Tests;

public sealed class OrderChannelAwareIssue199Tests : IntegrationTestBase
{
    [Fact]
    public void OrderChannel_PosCash_IsPosCounter()
        => Assert.Equal(OrderChannels.PosCounter,
            OrderChannelPolicy.Classify(OrderSources.Pos, SystemConstants.OrderTypes.DineIn));

    [Fact]
    public void OrderChannel_PosVietQr_IsPosCounter()
        => Assert.Equal(OrderChannels.PosCounter,
            OrderChannelPolicy.Classify(OrderSources.Pos, SystemConstants.OrderTypes.TakeAway));

    [Fact]
    public void OrderChannel_PosSplit_IsPosCounter()
        => Assert.Equal(OrderChannels.PosCounter,
            OrderChannelPolicy.Classify(OrderSources.Pos, SystemConstants.OrderTypes.Delivery));

    [Fact]
    public void OrderChannel_WebOrder_IsWeb()
        => Assert.Equal(OrderChannels.WebOrder,
            OrderChannelPolicy.Classify(OrderSources.Website, SystemConstants.OrderTypes.TakeAway));

    [Fact]
    public void OrderChannel_DeliveryOrder_IsDelivery()
        => Assert.Equal(OrderChannels.Delivery,
            OrderChannelPolicy.Classify(OrderSources.Website, SystemConstants.OrderTypes.Delivery));

    [Fact]
    public void OrderChannel_LegacyNull_IsUnknown()
        => Assert.Equal(OrderChannels.LegacyUnknown,
            OrderChannelPolicy.Classify(null, SystemConstants.OrderTypes.Delivery));

    [Fact]
    public void OrderChannel_DoesNotInferFromPaymentMethod()
    {
        Assert.Equal(OrderChannels.LegacyUnknown,
            OrderChannelPolicy.Classify("CASH", SystemConstants.OrderTypes.DineIn));
        Assert.Equal(OrderChannels.LegacyUnknown,
            OrderChannelPolicy.Classify("MOMO", SystemConstants.OrderTypes.Delivery));
    }

    [Theory]
    [InlineData("POS", SystemConstants.OrderTypes.DineIn, OrderChannels.PosCounter)]
    [InlineData("POS", SystemConstants.OrderTypes.TakeAway, OrderChannels.PosCounter)]
    [InlineData("Website", SystemConstants.OrderTypes.DineIn, OrderChannels.WebOrder)]
    [InlineData("Website", SystemConstants.OrderTypes.Delivery, OrderChannels.Delivery)]
    [InlineData(null, SystemConstants.OrderTypes.DineIn, OrderChannels.LegacyUnknown)]
    [InlineData("Other", SystemConstants.OrderTypes.Delivery, OrderChannels.LegacyUnknown)]
    public void Channel_policy_is_deterministic(string? source, int orderTypeId, string expected)
    {
        Assert.Equal(expected, OrderChannelPolicy.Classify(source, orderTypeId));
    }

    [Fact]
    public void Payment_display_handles_split_momo_and_unknown_without_raw_na()
    {
        Assert.Equal("Thanh toán kết hợp", OrderChannelPolicy.GetPaymentDisplay(new[] { "CASH", "BANK" }));
        Assert.Equal("Ví điện tử — dữ liệu cũ", OrderChannelPolicy.GetPaymentDisplay(new[] { "MOMO" }));
        Assert.Equal("Chưa xác định", OrderChannelPolicy.GetPaymentDisplay(new string?[] { null, "N/A" }));
    }

    [Fact]
    public void PaymentDisplay_CashVietnamese()
        => Assert.Equal("Tiền mặt", OrderChannelPolicy.GetPaymentDisplay(new[] { "CASH" }));

    [Theory]
    [InlineData("BANK")]
    [InlineData("BANK_TRANSFER")]
    [InlineData("VIETQR")]
    public void PaymentDisplay_VietQrVietnamese(string code)
        => Assert.Equal("Chuyển khoản VietQR", OrderChannelPolicy.GetPaymentDisplay(new[] { code }));

    [Fact]
    public void PaymentDisplay_SplitVietnamese()
        => Assert.Equal("Thanh toán kết hợp", OrderChannelPolicy.GetPaymentDisplay(new[] { "CASH", "BANK" }));

    [Fact]
    public void PaymentDisplay_LegacyMomo()
        => Assert.Equal("Ví điện tử — dữ liệu cũ", OrderChannelPolicy.GetPaymentDisplay(new[] { "MOMO" }));

    [Fact]
    public void PaymentDisplay_NullShowsChuaXacDinh()
        => Assert.Equal("Chưa xác định", OrderChannelPolicy.GetPaymentDisplay(new string?[] { null, "N/A" }));

    [Fact]
    public async Task Pos_history_contains_only_committed_paid_or_refunded_pos_sales_in_store()
    {
        await using var context = CreateDbContext();
        var paidPos = AddOrder(context, 1, "POS", SystemConstants.OrderStatuses.Completed, SystemConstants.PaymentStatuses.Paid);
        var refundedPos = AddOrder(context, 1, "POS", SystemConstants.OrderStatuses.Completed, SystemConstants.PaymentStatuses.Refunded);
        AddOrder(context, 1, "POS", SystemConstants.OrderStatuses.AwaitingPayment, SystemConstants.PaymentStatuses.Unpaid);
        AddOrder(context, 1, "Website", SystemConstants.OrderStatuses.Completed, SystemConstants.PaymentStatuses.Paid);
        AddOrder(context, 2, "POS", SystemConstants.OrderStatuses.Completed, SystemConstants.PaymentStatuses.Paid);
        await context.SaveChangesAsync();

        AddPayment(context, paidPos.OrderId, 1, SystemConstants.PaymentStatuses.Paid);
        AddPayment(context, refundedPos.OrderId, 1, SystemConstants.PaymentStatuses.Refunded);
        await context.SaveChangesAsync();

        var repository = new POSOrderRepository(context);
        var (items, total) = await repository.GetOrderHistoryAsync(1, 1, 20);

        Assert.Equal(2, total);
        Assert.Equal(new[] { paidPos.OrderId, refundedPos.OrderId }.Order(), items.Select(x => x.OrderId).Order());
        Assert.All(items, x => Assert.DoesNotContain("N/A", x.PaymentMethod, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PosApiHistory_UsesPaidAtForStablePagination_AndPreservesCashAudit()
    {
        await using var context = CreateDbContext();
        var paidLater = AddOrder(context, 1, OrderSources.Pos,
            SystemConstants.OrderStatuses.Completed, SystemConstants.PaymentStatuses.Paid,
            createdAt: new DateTime(2026, 7, 1, 8, 0, 0));
        var createdLater = AddOrder(context, 1, OrderSources.Pos,
            SystemConstants.OrderStatuses.Completed, SystemConstants.PaymentStatuses.Paid,
            createdAt: new DateTime(2026, 7, 22, 8, 0, 0));
        await context.SaveChangesAsync();

        context.Payments.AddRange(
            new Payment
            {
                OrderId = paidLater.OrderId,
                PaymentMethodId = 1,
                PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                Amount = 33_000m,
                ReceivedAmount = 50_000m,
                ChangeAmount = 17_000m,
                PaidAt = new DateTime(2026, 7, 23, 9, 0, 0)
            },
            new Payment
            {
                OrderId = paidLater.OrderId,
                PaymentMethodId = 3,
                PaymentStatusId = SystemConstants.PaymentStatuses.Failed,
                Amount = 33_000m,
                PaidAt = null
            },
            new Payment
            {
                OrderId = createdLater.OrderId,
                PaymentMethodId = 1,
                PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                Amount = 20_000m,
                PaidAt = new DateTime(2026, 7, 22, 9, 0, 0)
            });
        await context.SaveChangesAsync();

        var repository = new POSOrderRepository(context);
        var (firstPage, total) = await repository.GetOrderHistoryAsync(1, 1, 1);

        Assert.Equal(2, total);
        var row = Assert.Single(firstPage);
        Assert.Equal(paidLater.OrderId, row.OrderId);
        Assert.Equal(new DateTime(2026, 7, 23, 9, 0, 0), row.PaidAt);
        Assert.Equal("Tiền mặt", row.PaymentMethod);
        var cash = Assert.Single(row.Payments, payment => payment.PaymentStatusId == SystemConstants.PaymentStatuses.Paid);
        Assert.Equal(50_000m, cash.ReceivedAmount);
        Assert.Equal(17_000m, cash.ChangeAmount);
    }

    [Fact]
    public async Task Board_contains_only_website_orders_for_requested_store()
    {
        await using var context = CreateDbContext();
        var website = AddOrder(context, 1, "Website", SystemConstants.OrderStatuses.Pending, SystemConstants.PaymentStatuses.Unpaid);
        AddOrder(context, 1, "POS", SystemConstants.OrderStatuses.Pending, SystemConstants.PaymentStatuses.Unpaid);
        AddOrder(context, 2, "Website", SystemConstants.OrderStatuses.Pending, SystemConstants.PaymentStatuses.Unpaid);
        await context.SaveChangesAsync();

        var service = new AdminOrderService(
            context,
            new Mock<IHubContext<OrderHub>>().Object,
            new Mock<IInventoryService>().Object,
            new Mock<IOrderService>().Object);

        var rows = await service.GetKanbanOrdersAsync(1);

        Assert.Single(rows);
        Assert.Equal(website.OrderId, rows[0].OrderId);
    }

    [Fact]
    public async Task Board_action_transitions_website_order_but_rejects_pos_and_cross_store_orders()
    {
        await using var context = CreateDbContext();
        var website = AddOrder(context, 1, "Website", SystemConstants.OrderStatuses.Pending, SystemConstants.PaymentStatuses.Unpaid);
        var pos = AddOrder(context, 1, "POS", SystemConstants.OrderStatuses.Pending, SystemConstants.PaymentStatuses.Unpaid);
        var otherStore = AddOrder(context, 2, "Website", SystemConstants.OrderStatuses.Pending, SystemConstants.PaymentStatuses.Unpaid);
        await context.SaveChangesAsync();

        var service = CreateAdminOrderService(context);
        await service.AcceptOrderAsync(website.OrderId, 1);

        Assert.Equal(SystemConstants.OrderStatuses.Preparing, website.OrderStatusId);
        await Assert.ThrowsAsync<Exception>(() => service.AcceptOrderAsync(pos.OrderId, 1));
        await Assert.ThrowsAsync<Exception>(() => service.AcceptOrderAsync(otherStore.OrderId, 1));
    }

    [Fact]
    public async Task Board_ExcludesPosAwaitingPayment_AndIncludesDeliveryDelivering()
    {
        await using var context = CreateDbContext();
        var delivery = AddOrder(context, 1, OrderSources.Website,
            SystemConstants.OrderStatuses.Delivering, SystemConstants.PaymentStatuses.Paid,
            SystemConstants.OrderTypes.Delivery);
        AddOrder(context, 1, OrderSources.Pos,
            SystemConstants.OrderStatuses.AwaitingPayment, SystemConstants.PaymentStatuses.Unpaid);
        await context.SaveChangesAsync();

        var rows = await CreateAdminOrderService(context).GetKanbanOrdersAsync(1);

        Assert.Single(rows);
        Assert.Equal(delivery.OrderId, rows[0].OrderId);
    }

    [Fact]
    public async Task Board_WebTransitionStillWorks()
    {
        await using var context = CreateDbContext();
        var order = AddOrder(context, 1, OrderSources.Website,
            SystemConstants.OrderStatuses.Pending, SystemConstants.PaymentStatuses.Unpaid,
            SystemConstants.OrderTypes.TakeAway);
        await context.SaveChangesAsync();
        var service = CreateAdminOrderService(context);

        await service.AcceptOrderAsync(order.OrderId, 1);
        await service.ReadyForPickupAsync(order.OrderId, 1);

        Assert.Equal(SystemConstants.OrderStatuses.Ready, order.OrderStatusId);
    }

    [Fact]
    public async Task Board_DeliveryTransitionStillWorks()
    {
        await using var context = CreateDbContext();
        var order = AddOrder(context, 1, OrderSources.Website,
            SystemConstants.OrderStatuses.Delivering, SystemConstants.PaymentStatuses.Unpaid,
            SystemConstants.OrderTypes.Delivery);
        await context.SaveChangesAsync();
        var payment = new Payment
        {
            OrderId = order.OrderId,
            Amount = order.Total,
            PaymentMethodId = 1,
            PaymentStatusId = SystemConstants.PaymentStatuses.Unpaid
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        await CreateAdminOrderService(context).CompleteOrderAsync(order.OrderId, 1);

        Assert.Equal(SystemConstants.OrderStatuses.Completed, order.OrderStatusId);
        Assert.Equal(SystemConstants.PaymentStatuses.Paid, order.PaymentStatusId);
        Assert.Equal(SystemConstants.PaymentStatuses.Paid, payment.PaymentStatusId);
        Assert.NotNull(payment.PaidAt);
    }

    [Fact]
    public async Task Admin_sales_history_is_pos_paid_only_and_preserves_split_payment_summary()
    {
        await using var context = CreateDbContext();
        var pos = AddOrder(context, 1, "POS", SystemConstants.OrderStatuses.Completed, SystemConstants.PaymentStatuses.Paid);
        var website = AddOrder(context, 1, "Website", SystemConstants.OrderStatuses.Completed, SystemConstants.PaymentStatuses.Paid);
        await context.SaveChangesAsync();
        AddPayment(context, pos.OrderId, 1, SystemConstants.PaymentStatuses.Paid);
        AddPayment(context, pos.OrderId, 2, SystemConstants.PaymentStatuses.Paid);
        AddPayment(context, website.OrderId, 1, SystemConstants.PaymentStatuses.Paid);
        await context.SaveChangesAsync();

        var rows = await CreateAdminOrderService(context).GetFilteredOrdersForExportAsync(
            string.Empty, string.Empty, string.Empty, null, null, 1);

        var row = Assert.Single(rows);
        Assert.Equal(pos.OrderId, row.OrderId);
        Assert.Equal("Thanh toán kết hợp", row.PaymentMethodName);
        Assert.Equal("Đã thanh toán", row.OrderStatusName);
    }

    [Theory]
    [InlineData(1, "Tiền mặt")]
    [InlineData(2, "Chuyển khoản VietQR")]
    public async Task PosHistory_IncludesCommittedPaidSingleTender(int methodId, string expectedLabel)
    {
        await using var context = CreateDbContext();
        var order = AddOrder(context, 1, OrderSources.Pos,
            SystemConstants.OrderStatuses.Completed, SystemConstants.PaymentStatuses.Paid);
        await context.SaveChangesAsync();
        AddPayment(context, order.OrderId, methodId, SystemConstants.PaymentStatuses.Paid);
        await context.SaveChangesAsync();

        var row = Assert.Single(await CreateAdminOrderService(context).GetFilteredOrdersForExportAsync(
            string.Empty, string.Empty, string.Empty, null, null, 1));

        Assert.Equal(expectedLabel, row.PaymentMethodName);
    }

    [Fact]
    public async Task PosHistory_ExcludesAwaitingPayment_CancelledBeforePayment_AndWebDelivery()
    {
        await using var context = CreateDbContext();
        AddOrder(context, 1, OrderSources.Pos,
            SystemConstants.OrderStatuses.AwaitingPayment, SystemConstants.PaymentStatuses.Unpaid);
        AddOrder(context, 1, OrderSources.Pos,
            SystemConstants.OrderStatuses.Cancelled, SystemConstants.PaymentStatuses.Failed);
        AddOrder(context, 1, OrderSources.Website,
            SystemConstants.OrderStatuses.Completed, SystemConstants.PaymentStatuses.Paid,
            SystemConstants.OrderTypes.Delivery);
        await context.SaveChangesAsync();

        var rows = await CreateAdminOrderService(context).GetFilteredOrdersForExportAsync(
            string.Empty, string.Empty, string.Empty, null, null, 1);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task PosHistory_DateRangeUsesPaidAt()
    {
        await using var context = CreateDbContext();
        var targetDate = new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Local);
        var paidInRange = AddOrder(context, 1, OrderSources.Pos,
            SystemConstants.OrderStatuses.Completed, SystemConstants.PaymentStatuses.Paid,
            createdAt: targetDate.AddDays(-30));
        var createdInRangeButPaidOutside = AddOrder(context, 1, OrderSources.Pos,
            SystemConstants.OrderStatuses.Completed, SystemConstants.PaymentStatuses.Paid,
            createdAt: targetDate);
        await context.SaveChangesAsync();
        AddPayment(context, paidInRange.OrderId, 1, SystemConstants.PaymentStatuses.Paid, targetDate);
        AddPayment(context, createdInRangeButPaidOutside.OrderId, 1, SystemConstants.PaymentStatuses.Paid, targetDate.AddDays(-30));
        await context.SaveChangesAsync();

        var rows = await CreateAdminOrderService(context).GetFilteredOrdersForExportAsync(
            string.Empty, "2026-07-20", "2026-07-20", null, null, 1);

        var row = Assert.Single(rows);
        Assert.Equal(paidInRange.OrderId, row.OrderId);
        Assert.Equal(targetDate, row.CreatedAt);
    }

    [Fact]
    public async Task PosHistory_PaginationStable()
    {
        await using var context = CreateDbContext();
        var first = AddOrder(context, 1, OrderSources.Pos,
            SystemConstants.OrderStatuses.Completed, SystemConstants.PaymentStatuses.Paid);
        var second = AddOrder(context, 1, OrderSources.Pos,
            SystemConstants.OrderStatuses.Completed, SystemConstants.PaymentStatuses.Paid);
        await context.SaveChangesAsync();
        AddPayment(context, first.OrderId, 1, SystemConstants.PaymentStatuses.Paid, new DateTime(2026, 7, 20, 10, 0, 0));
        AddPayment(context, second.OrderId, 1, SystemConstants.PaymentStatuses.Paid, new DateTime(2026, 7, 20, 11, 0, 0));
        await context.SaveChangesAsync();

        var result = await CreateAdminOrderService(context).GetOrderHistoryAsync(new CafeChain.Application.DTOs.Admin.DataTablesRequest
        {
            Draw = 7,
            Start = 1,
            Length = 1,
            Order = new() { new() { Column = 0, Dir = "desc" } },
            Columns = new() { new() { Data = "orderId" } }
        }, 1);

        Assert.Equal(7, result.Draw);
        Assert.Equal(2, result.RecordsTotal);
        Assert.Equal(2, result.RecordsFiltered);
        Assert.Single(result.Data);
        Assert.Equal(first.OrderId, result.Data[0].OrderId);
    }

    [Fact]
    public async Task PosHistory_StoreScopeEnforced_AndRevenueCountsPaidOnly()
    {
        await using var context = CreateDbContext();
        var paid = AddOrder(context, 1, OrderSources.Pos,
            SystemConstants.OrderStatuses.Completed, SystemConstants.PaymentStatuses.Paid,
            total: 33_000m);
        var refunded = AddOrder(context, 1, OrderSources.Pos,
            SystemConstants.OrderStatuses.Completed, SystemConstants.PaymentStatuses.Refunded,
            total: 20_000m);
        var otherStore = AddOrder(context, 2, OrderSources.Pos,
            SystemConstants.OrderStatuses.Completed, SystemConstants.PaymentStatuses.Paid,
            total: 99_000m);
        await context.SaveChangesAsync();
        AddPayment(context, paid.OrderId, 1, SystemConstants.PaymentStatuses.Paid);
        AddPayment(context, refunded.OrderId, 1, SystemConstants.PaymentStatuses.Refunded);
        AddPayment(context, otherStore.OrderId, 1, SystemConstants.PaymentStatuses.Paid);
        await context.SaveChangesAsync();

        var rows = await CreateAdminOrderService(context).GetFilteredOrdersForExportAsync(
            string.Empty, string.Empty, string.Empty, null, null, 1);

        Assert.Equal(2, rows.Count);
        Assert.Equal(33_000m, rows.Where(x => x.OrderStatusId == SystemConstants.PaymentStatuses.Paid).Sum(x => x.Total));
        Assert.DoesNotContain(rows, x => x.OrderId == otherStore.OrderId);
    }

    [Fact]
    public async Task PosStatus_RefundedOnlyWithEvidence()
    {
        await using var context = CreateDbContext();
        var withEvidence = AddOrder(context, 1, OrderSources.Pos,
            SystemConstants.OrderStatuses.Completed, SystemConstants.PaymentStatuses.Refunded);
        AddOrder(context, 1, OrderSources.Pos,
            SystemConstants.OrderStatuses.Completed, SystemConstants.PaymentStatuses.Refunded);
        await context.SaveChangesAsync();
        AddPayment(context, withEvidence.OrderId, 1, SystemConstants.PaymentStatuses.Refunded);
        await context.SaveChangesAsync();

        var row = Assert.Single(await CreateAdminOrderService(context).GetFilteredOrdersForExportAsync(
            string.Empty, string.Empty, string.Empty, null, null, 1));

        Assert.Equal("Đã hoàn tiền", row.OrderStatusName);
    }

    private static Order AddOrder(
        CafeChain.Data.AppDbContext context,
        int storeId,
        string? source,
        int orderStatusId,
        int paymentStatusId,
        int? orderTypeId = null,
        DateTime? createdAt = null,
        decimal total = 20_000m)
    {
        var order = new Order
        {
            StoreId = storeId,
            Source = source,
            OrderStatusId = orderStatusId,
            PaymentStatusId = paymentStatusId,
            OrderTypeId = orderTypeId ?? (source == OrderSources.Website
                ? SystemConstants.OrderTypes.Delivery
                : SystemConstants.OrderTypes.DineIn),
            SubTotal = total,
            Total = total,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            OrderDetails = new List<OrderDetail>(),
            Payments = new List<Payment>()
        };
        context.Orders.Add(order);
        return order;
    }

    private static void AddPayment(
        CafeChain.Data.AppDbContext context,
        int orderId,
        int methodId,
        int statusId,
        DateTime? paidAt = null,
        decimal amount = 20_000m)
    {
        context.Payments.Add(new Payment
        {
            OrderId = orderId,
            Amount = amount,
            PaymentMethodId = methodId,
            PaymentStatusId = statusId,
            PaidAt = paidAt ?? DateTime.UtcNow
        });
    }

    private static AdminOrderService CreateAdminOrderService(CafeChain.Data.AppDbContext context)
    {
        var client = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.Setup(x => x.Group("AdminDashboard")).Returns(client.Object);
        var hub = new Mock<IHubContext<OrderHub>>();
        hub.SetupGet(x => x.Clients).Returns(clients.Object);

        return new AdminOrderService(
            context,
            hub.Object,
            new Mock<IInventoryService>().Object,
            new Mock<IOrderService>().Object);
    }
}

public sealed class OrderChannelAwareIssue199SourceTests
{
    [Fact]
    public void Website_checkout_preserves_takeaway_or_delivery_channel_authority()
    {
        var service = Read("CafeChain", "Application", "Services", "Order", "OrderService.cs");
        var checkout = Read("CafeChain", "Views", "Checkout", "Index.cshtml");

        Assert.Contains("model.OrderTypeId != SystemConstants.OrderTypes.TakeAway", service, StringComparison.Ordinal);
        Assert.Contains("OrderTypeId = model.OrderTypeId", service, StringComparison.Ordinal);
        Assert.Contains("DeliveryAddress = isDeliveryOrder ? address.DisplayAddress : null", service, StringComparison.Ordinal);
        Assert.Contains("ShippingFee = isDeliveryOrder ? 15000 : 0", service, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"OrderTypeId\"", checkout, StringComparison.Ordinal);
    }

    [Fact]
    public void PosDrawer_MapsCashAuditAndIgnoresFailedTenderInSummary()
    {
        var frontend = Read("CafeChain.Frontend", "src", "pages", "OrderHistory.tsx");
        var dto = Read("CafeChain", "Application", "DTOs", "POS", "POSOrderHistoryDto.cs");
        var repository = Read("CafeChain", "Infrastructure", "Repositories", "Admin", "POS", "POSOrderRepository.cs");

        Assert.Contains("receivedAmount: safeMoney(payment?.receivedAmount)", frontend, StringComparison.Ordinal);
        Assert.Contains("changeAmount: safeMoney(payment?.changeAmount)", frontend, StringComparison.Ordinal);
        Assert.Contains("const settledPayments = payments.filter", frontend, StringComparison.Ordinal);
        Assert.Contains("public decimal? ReceivedAmount", dto, StringComparison.Ordinal);
        Assert.Contains("public decimal? ChangeAmount", dto, StringComparison.Ordinal);
        Assert.Contains("ReceivedAmount = p.ReceivedAmount", repository, StringComparison.Ordinal);
        Assert.Contains("ChangeAmount = p.ChangeAmount", repository, StringComparison.Ordinal);
    }

    [Fact]
    public void Board_AuthorizationPreserved()
    {
        var controller = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminOrderController.cs");
        var baseController = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminBaseController.cs");

        Assert.Contains("class AdminOrderController : AdminBaseController", controller, StringComparison.Ordinal);
        Assert.Contains("[Authorize(Policy = \"RequireAdminPanelAccess\")]", baseController, StringComparison.Ordinal);
        Assert.Contains("ResolveStoreIdAsync", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void WebStatus_PreparingPreserved()
    {
        var service = Read("CafeChain", "Application", "Services", "Admin", "Order", "AdminOrderService.cs");
        Assert.Contains("OrderStatuses.Preparing", service, StringComparison.Ordinal);
        Assert.Contains("AcceptOrderAsync", service, StringComparison.Ordinal);
        Assert.Contains("ReadyForPickupAsync", service, StringComparison.Ordinal);
    }

    [Fact]
    public void DeliveryStatus_DeliveringPreserved()
    {
        var service = Read("CafeChain", "Application", "Services", "Admin", "Order", "AdminOrderService.cs");
        Assert.Contains("OrderStatuses.Delivering", service, StringComparison.Ordinal);
        Assert.Contains("DispatchOrderAsync", service, StringComparison.Ordinal);
        Assert.Contains("SimulateWebhookAsync", service, StringComparison.Ordinal);
    }

    [Fact]
    public void PosStatus_VoidedOnlyWithEvidence()
    {
        var policy = Read("CafeChain", "Application", "Policies", "Orders", "OrderChannelPolicy.cs");
        var adminHistory = Read("CafeChain", "Application", "Services", "Admin", "Order", "AdminOrderService.cs");

        Assert.DoesNotContain("Đã vô hiệu hóa", policy + adminHistory, StringComparison.Ordinal);
        Assert.Contains("PaymentStatuses.Refunded", adminHistory, StringComparison.Ordinal);
    }

    [Fact]
    public void PosStatus_LegacyUnknownNeedsReview()
    {
        var report = Read("CafeChain", "docs", "analysis", "prepaid-order-management-status-review.md");
        Assert.Contains("LEGACY_UNKNOWN", report, StringComparison.Ordinal);
        Assert.Contains("MANUAL_REVIEW_REQUIRED", report, StringComparison.Ordinal);
    }

    [Fact]
    public void PosCsv_ContainsOnlyPosCommittedSales_AndExcludesAwaitingPayment()
    {
        var service = Read("CafeChain", "Application", "Services", "Admin", "Order", "AdminOrderService.cs");
        var controller = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminOrderController.cs");

        Assert.Contains("GetFilteredOrdersForExportAsync", controller, StringComparison.Ordinal);
        Assert.Contains("o.Source == OrderSources.Pos", service, StringComparison.Ordinal);
        Assert.Contains("o.OrderStatusId == SystemConstants.OrderStatuses.Completed", service, StringComparison.Ordinal);
        Assert.DoesNotContain("OrderStatuses.AwaitingPayment", service[service.IndexOf("BuildHistoryQuery", StringComparison.Ordinal)..], StringComparison.Ordinal);
    }

    [Fact]
    public void PosCsv_UsesVietnamesePaymentLabels()
    {
        var controller = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminOrderController.cs");
        var policy = Read("CafeChain", "Application", "Policies", "Orders", "OrderChannelPolicy.cs");

        Assert.Contains("Csv(o.PaymentMethodName)", controller, StringComparison.Ordinal);
        Assert.Contains("Chuyển khoản VietQR", policy, StringComparison.Ordinal);
        Assert.Contains("Ví điện tử — dữ liệu cũ", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("\"N/A\"", policy, StringComparison.Ordinal);
    }

    [Fact]
    public void PosCsv_RevenueMatchesHistorySummary()
    {
        var controller = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminOrderController.cs");
        var service = Read("CafeChain", "Application", "Services", "Admin", "Order", "AdminOrderService.cs");

        Assert.Contains("GetFilteredOrdersForExportAsync", controller, StringComparison.Ordinal);
        Assert.Contains("GetPosSalesHistoryAsync", controller, StringComparison.Ordinal);
        Assert.Contains("PaidRevenue = paidRevenue", service, StringComparison.Ordinal);
        Assert.Contains("PaymentStatusId == SystemConstants.PaymentStatuses.Paid", service, StringComparison.Ordinal);
        Assert.Contains("var data = await _adminOrderService.GetFilteredOrdersForExportAsync", controller, StringComparison.Ordinal);
    }

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([FindRepoRoot(), .. path]));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "CafeChain"))
                && Directory.Exists(Path.Combine(directory.FullName, "CafeChain.Tests")))
                return directory.FullName;
            directory = directory.Parent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
