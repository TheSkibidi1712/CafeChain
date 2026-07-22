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

    private static Order AddOrder(
        CafeChain.Data.AppDbContext context,
        int storeId,
        string? source,
        int orderStatusId,
        int paymentStatusId)
    {
        var order = new Order
        {
            StoreId = storeId,
            Source = source,
            OrderStatusId = orderStatusId,
            PaymentStatusId = paymentStatusId,
            OrderTypeId = source == "Website" ? SystemConstants.OrderTypes.Delivery : SystemConstants.OrderTypes.DineIn,
            SubTotal = 20_000m,
            Total = 20_000m,
            CreatedAt = DateTime.UtcNow,
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
        int statusId)
    {
        context.Payments.Add(new Payment
        {
            OrderId = orderId,
            Amount = 20_000m,
            PaymentMethodId = methodId,
            PaymentStatusId = statusId,
            PaidAt = DateTime.UtcNow
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
