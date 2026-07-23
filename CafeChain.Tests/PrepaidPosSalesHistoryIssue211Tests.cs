using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces;
using CafeChain.Application.Policies.Orders;
using CafeChain.Application.Services.Admin;
using CafeChain.Hubs;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class PrepaidPosSalesHistoryIssue211Tests : IntegrationTestBase
{
    [Fact]
    public async Task History_IncludesCommittedCashVietQrAndSplit()
    {
        await using var context = CreateDbContext();
        var cash = AddOrder(context, 1, OrderSources.Pos, Completed, Paid, 25_000m);
        var qr = AddOrder(context, 1, OrderSources.Pos, Completed, Paid, 30_000m);
        var split = AddOrder(context, 1, OrderSources.Pos, Completed, Paid, 45_000m);
        await context.SaveChangesAsync();
        AddPayment(context, cash, 1, Paid, 25_000m);
        AddPayment(context, cash, 2, SystemConstants.PaymentStatuses.Failed, 25_000m);
        AddPayment(context, qr, 2, Paid, 30_000m);
        AddPayment(context, split, 1, Paid, 15_000m);
        AddPayment(context, split, 2, Paid, 30_000m);
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var page = await Page(service, new[] { 1 });
        var detail = await service.GetOrderHistoryDetailAsync(split.OrderId, 1);

        Assert.Equal(3, page.TotalItems);
        Assert.Contains(page.Items, x =>
            x.OrderId == cash.OrderId && x.PaymentMethodName == "Tiền mặt");
        Assert.Contains(page.Items, x =>
            x.OrderId == qr.OrderId && x.PaymentMethodName == "Chuyển khoản VietQR");
        Assert.Contains(page.Items, x =>
            x.OrderId == split.OrderId && x.PaymentMethodName == "Thanh toán kết hợp");
        Assert.NotNull(detail);
        Assert.Equal(2, detail!.Payments.Count);
        Assert.Equal(45_000m, detail.Payments.Sum(x => x.Amount));
    }

    [Fact]
    public async Task History_ExcludesAwaitingPaymentCancelledBeforePaymentAndWebsiteDelivery()
    {
        await using var context = CreateDbContext();
        var sold = AddOrder(context, 1, OrderSources.Pos, Completed, Paid, 20_000m);
        var awaiting = AddOrder(
            context,
            1,
            OrderSources.Pos,
            SystemConstants.OrderStatuses.AwaitingPayment,
            SystemConstants.PaymentStatuses.Unpaid,
            30_000m);
        var cancelled = AddOrder(
            context,
            1,
            OrderSources.Pos,
            SystemConstants.OrderStatuses.Cancelled,
            SystemConstants.PaymentStatuses.Failed,
            40_000m);
        var delivery = AddOrder(
            context,
            1,
            OrderSources.Website,
            Completed,
            Paid,
            50_000m,
            SystemConstants.OrderTypes.Delivery);
        await context.SaveChangesAsync();
        AddPayment(context, sold, 1, Paid, 20_000m);
        AddPayment(context, awaiting, 2, SystemConstants.PaymentStatuses.Unpaid, 30_000m);
        AddPayment(context, cancelled, 1, SystemConstants.PaymentStatuses.Failed, 40_000m);
        AddPayment(context, delivery, 1, Paid, 50_000m);
        await context.SaveChangesAsync();

        var page = await Page(CreateService(context), new[] { 1 });

        Assert.Equal(sold.OrderId, Assert.Single(page.Items).OrderId);
        Assert.Equal(1, page.Stats.PaidOrders);
        Assert.Equal(20_000m, page.Stats.PaidRevenue);
    }

    [Fact]
    public async Task History_KpiAndExportMatchOneStoreOrAllAllowedStores()
    {
        await using var context = CreateDbContext();
        var storeAPaid = AddOrder(context, 1, OrderSources.Pos, Completed, Paid, 25_000m);
        var storeARefund = AddOrder(context, 1, OrderSources.Pos, Completed, Refunded, 15_000m);
        var storeBPaid = AddOrder(context, 2, OrderSources.Pos, Completed, Paid, 40_000m);
        var outside = AddOrder(context, 3, OrderSources.Pos, Completed, Paid, 99_000m);
        await context.SaveChangesAsync();
        AddPayment(context, storeAPaid, 1, Paid, 25_000m);
        AddPayment(context, storeARefund, 1, Refunded, 15_000m);
        AddPayment(context, storeBPaid, 2, Paid, 40_000m);
        AddPayment(context, outside, 1, Paid, 99_000m);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var storeA = await Page(service, new[] { 1 });
        var allAllowed = await Page(service, new[] { 1, 2 });
        var exported = await service.GetFilteredOrdersForExportAsync(
            "", "", "", null, null, new[] { 1, 2 });

        Assert.Equal(25_000m, storeA.Stats.PaidRevenue);
        Assert.Equal(15_000m, storeA.Stats.RefundedAmount);
        Assert.Equal(65_000m, allAllowed.Stats.PaidRevenue);
        Assert.Equal(
            allAllowed.Items.Select(x => x.OrderId).Order(),
            exported.Select(x => x.OrderId).Order());
        Assert.DoesNotContain(allAllowed.Items, x => x.OrderId == outside.OrderId);
    }

    [Fact]
    public async Task History_PaginationIsStableAndUsesPaidAt()
    {
        await using var context = CreateDbContext();
        var sameTime = new DateTime(2026, 7, 24, 8, 0, 0, DateTimeKind.Utc);
        var orders = Enumerable.Range(0, 12)
            .Select(index => AddOrder(
                context,
                1,
                OrderSources.Pos,
                Completed,
                Paid,
                10_000m + index,
                createdAt: sameTime.AddDays(-10)))
            .ToList();
        await context.SaveChangesAsync();
        foreach (var order in orders)
            AddPayment(context, order, 1, Paid, order.Total, sameTime);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var first = await service.GetPosSalesHistoryAsync(
            1, 10, "", "", "", null, null, new[] { 1 });
        var second = await service.GetPosSalesHistoryAsync(
            2, 10, "", "", "", null, null, new[] { 1 });

        Assert.Equal(12, first.TotalItems);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(10, first.Items.Count);
        Assert.Equal(2, second.Items.Count);
        Assert.Empty(first.Items.Select(x => x.OrderId).Intersect(
            second.Items.Select(x => x.OrderId)));
        Assert.All(first.Items.Concat(second.Items), x => Assert.Equal(sameTime, x.CreatedAt));
        Assert.True(first.Items.Zip(first.Items.Skip(1))
            .All(pair => pair.First.OrderId > pair.Second.OrderId));
    }

    [Fact]
    public async Task History_LegacyMomoAndUnknownPaymentAreSafe()
    {
        await using var context = CreateDbContext();
        var momo = AddOrder(context, 1, OrderSources.Pos, Completed, Paid, 10_000m);
        var unknown = AddOrder(context, 1, OrderSources.Pos, Completed, Paid, 11_000m);
        await context.SaveChangesAsync();
        AddPayment(context, momo, 3, Paid, 10_000m);
        AddPayment(context, unknown, 999, Paid, 11_000m);
        await context.SaveChangesAsync();

        var page = await Page(CreateService(context), new[] { 1 });

        Assert.Contains(page.Items, x =>
            x.OrderId == momo.OrderId && x.PaymentMethodName == "Ví điện tử — dữ liệu cũ");
        Assert.Contains(page.Items, x =>
            x.OrderId == unknown.OrderId && x.PaymentMethodName == "Chưa xác định");
    }

    [Fact]
    public async Task History_PrintAndInventoryStatesUsePersistedEvidenceOnly()
    {
        await using var context = CreateDbContext();
        var posted = AddOrder(context, 1, OrderSources.Pos, Completed, Paid, 20_000m);
        var unposted = AddOrder(context, 1, OrderSources.Pos, Completed, Paid, 21_000m);
        await context.SaveChangesAsync();
        AddPayment(context, posted, 1, Paid, 20_000m);
        AddPayment(context, unposted, 1, Paid, 21_000m);
        context.InventoryTransactions.Add(new InventoryTransaction
        {
            StoreInventoryId = 999,
            ReferenceOrderId = posted.OrderId,
            Type = InventoryTransactionTypeEnum.SALES_DEDUCTION,
            StockStatus = InventoryStockStatus.NORMAL,
            Quantity = -1,
            BeforeQty = 5,
            AfterQty = 4,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var page = await Page(CreateService(context), new[] { 1 });

        Assert.Contains(page.Items, x =>
            x.OrderId == posted.OrderId
            && x.InventoryPostingState == "Đã ghi nhận"
            && x.ReceiptState == "Chưa có dữ liệu in"
            && x.DrinkLabelState == "Chưa có dữ liệu in");
        Assert.Contains(page.Items, x =>
            x.OrderId == unposted.OrderId
            && x.InventoryPostingState == "Chưa có dữ liệu kho");
    }

    private static int Completed => SystemConstants.OrderStatuses.Completed;
    private static int Paid => SystemConstants.PaymentStatuses.Paid;
    private static int Refunded => SystemConstants.PaymentStatuses.Refunded;

    private static Task<CafeChain.Application.DTOs.Admin.AdminOrderHistoryPageDto> Page(
        AdminOrderService service,
        IReadOnlyCollection<int> storeIds) =>
        service.GetPosSalesHistoryAsync(
            1, 20, "", "", "", null, null, storeIds);

    private static Order AddOrder(
        CafeChain.Data.AppDbContext context,
        int storeId,
        string source,
        int orderStatus,
        int paymentStatus,
        decimal total,
        int orderType = SystemConstants.OrderTypes.DineIn,
        DateTime? createdAt = null)
    {
        var order = new Order
        {
            StoreId = storeId,
            Source = source,
            OrderStatusId = orderStatus,
            PaymentStatusId = paymentStatus,
            OrderTypeId = orderType,
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
        Order order,
        int methodId,
        int statusId,
        decimal amount,
        DateTime? paidAt = null)
    {
        context.Payments.Add(new Payment
        {
            OrderId = order.OrderId,
            PaymentMethodId = methodId,
            PaymentStatusId = statusId,
            Amount = amount,
            PaidAt = paidAt ?? DateTime.UtcNow
        });
    }

    private static AdminOrderService CreateService(
        CafeChain.Data.AppDbContext context)
    {
        var client = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.Setup(x => x.Group("AdminDashboard")).Returns(client.Object);
        var hub = new Mock<IHubContext<OrderHub>>();
        hub.SetupGet(x => x.Clients).Returns(clients.Object);

        return new AdminOrderService(
            context,
            hub.Object,
            Mock.Of<IInventoryService>(),
            Mock.Of<IOrderService>());
    }
}

public sealed class PrepaidPosSalesHistoryIssue211SourceTests
{
    [Fact]
    public void HistoryUi_UsesServerStoresAndContainsNoDeliveryWorkflow()
    {
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AdminOrder", "History.cshtml");

        Assert.Contains("ViewBag.StoreOptions", view, StringComparison.Ordinal);
        Assert.Contains("Tất cả cửa hàng trong phạm vi", view, StringComparison.Ordinal);
        Assert.Contains("allWithinScope", view, StringComparison.Ordinal);
        Assert.Contains("data-store-id", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Khách hàng", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Số điện thoại", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Đang giao hàng", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Shipper", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HistoryDrawer_ShowsAllSeparatedOperationalStates()
    {
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AdminOrder", "History.cshtml");
        var service = Read("CafeChain", "Application", "Services", "Admin", "Order", "AdminOrderService.cs");

        Assert.Contains("detail.payments || []", view, StringComparison.Ordinal);
        Assert.Contains("receivedAmount", view, StringComparison.Ordinal);
        Assert.Contains("changeAmount", view, StringComparison.Ordinal);
        Assert.Contains("detail.syncState", view, StringComparison.Ordinal);
        Assert.Contains("detail.inventoryPostingState", view, StringComparison.Ordinal);
        Assert.Contains("Chưa có dữ liệu in", service, StringComparison.Ordinal);
        var historyStart = service.IndexOf(
            "GetPosSalesHistoryAsync",
            StringComparison.Ordinal);
        var detailStart = service.IndexOf(
            "GetOrderHistoryDetailAsync",
            historyStart,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Payments.FirstOrDefault()",
            service[historyStart..detailStart],
            StringComparison.Ordinal);
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

        throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
