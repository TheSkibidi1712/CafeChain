using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces;
using CafeChain.Application.Services.Admin;
using CafeChain.Data;
using CafeChain.Hubs;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class OrderChannelAwareIssue199SqlServerTests : IAsyncLifetime
{
    private const string Database = "CafeChain_OrderChannelIssue199Tests";
    private const int StoreA = 1;
    private const int StoreB = 2;

    private int _posAwaitingId;
    private int _posPaidId;
    private int _posRefundedId;
    private int _webPendingId;
    private int _deliveryId;
    private int _otherStorePosId;
    private int _legacyId;

    private static string ConnectionString => SqlServerTestConnection.Create(Database);

    public async Task InitializeAsync()
    {
        try
        {
            await using (var master = new SqlConnection(SqlServerTestConnection.MasterConnectionString()))
            {
                await master.OpenAsync();
                await using var command = master.CreateCommand();
                command.CommandText = $"IF DB_ID(N'{Database}') IS NULL CREATE DATABASE [{Database}];";
                await command.ExecuteNonQueryAsync();
            }

            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
            await SeedAsync(context);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"SQL Server integration environment unavailable. Database={Database}. {ex.Message}", ex);
        }
    }

    public async Task DisposeAsync()
    {
        try
        {
            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
        }
        catch
        {
            // The test result already carries the environment failure; cleanup is best effort.
        }
    }

    [Fact]
    public async Task SqlServer_PosAwaitingPayment_NotInSalesHistory()
    {
        await using var context = CreateContext();
        var rows = await CreateService(context).GetFilteredOrdersForExportAsync(
            string.Empty, string.Empty, string.Empty, null, null, StoreA);

        Assert.DoesNotContain(rows, x => x.OrderId == _posAwaitingId);
    }

    [Fact]
    public async Task SqlServer_PosPaidOrder_InHistoryNotBoard()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var history = await service.GetFilteredOrdersForExportAsync(
            string.Empty, string.Empty, string.Empty, null, null, StoreA);
        var board = await service.GetKanbanOrdersAsync(StoreA);

        Assert.Contains(history, x => x.OrderId == _posPaidId);
        Assert.DoesNotContain(board, x => x.OrderId == _posPaidId);
    }

    [Fact]
    public async Task SqlServer_WebDelivery_InBoardNotPosHistory()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var history = await service.GetFilteredOrdersForExportAsync(
            string.Empty, string.Empty, string.Empty, null, null, StoreA);
        var board = await service.GetKanbanOrdersAsync(StoreA);

        Assert.Contains(board, x => x.OrderId == _webPendingId);
        Assert.Contains(board, x => x.OrderId == _deliveryId);
        Assert.DoesNotContain(history, x => x.OrderId == _webPendingId || x.OrderId == _deliveryId);
    }

    [Fact]
    public async Task SqlServer_RevenuePaidOnly()
    {
        await using var context = CreateContext();
        var rows = await CreateService(context).GetFilteredOrdersForExportAsync(
            string.Empty, string.Empty, string.Empty, null, null, StoreA);

        Assert.Contains(rows, x => x.OrderId == _posRefundedId);
        Assert.Equal(33_000m,
            rows.Where(x => x.OrderStatusId == SystemConstants.PaymentStatuses.Paid).Sum(x => x.Total));
    }

    [Fact]
    public async Task SqlServer_StoreScopeNoLeak()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var history = await service.GetFilteredOrdersForExportAsync(
            string.Empty, string.Empty, string.Empty, null, null, StoreA);
        var board = await service.GetKanbanOrdersAsync(StoreA);

        Assert.DoesNotContain(history, x => x.OrderId == _otherStorePosId);
        Assert.DoesNotContain(board, x => x.OrderId == _otherStorePosId);
    }

    [Fact]
    public async Task SqlServer_ChannelLegacyUnknownHandled()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var history = await service.GetFilteredOrdersForExportAsync(
            string.Empty, string.Empty, string.Empty, null, null, StoreA);
        var board = await service.GetKanbanOrdersAsync(StoreA);

        Assert.DoesNotContain(history, x => x.OrderId == _legacyId);
        Assert.DoesNotContain(board, x => x.OrderId == _legacyId);
    }

    private async Task SeedAsync(AppDbContext context)
    {
        var now = DateTime.Now;
        var posAwaiting = NewOrder(StoreA, "POS", SystemConstants.OrderStatuses.AwaitingPayment,
            SystemConstants.PaymentStatuses.Unpaid, SystemConstants.OrderTypes.TakeAway, 33_000m, now.AddMinutes(-6));
        var posPaid = NewOrder(StoreA, "POS", SystemConstants.OrderStatuses.Completed,
            SystemConstants.PaymentStatuses.Paid, SystemConstants.OrderTypes.TakeAway, 33_000m, now.AddMinutes(-5));
        var posRefunded = NewOrder(StoreA, "POS", SystemConstants.OrderStatuses.Completed,
            SystemConstants.PaymentStatuses.Refunded, SystemConstants.OrderTypes.DineIn, 20_000m, now.AddMinutes(-4));
        var webPending = NewOrder(StoreA, "Website", SystemConstants.OrderStatuses.Pending,
            SystemConstants.PaymentStatuses.Unpaid, SystemConstants.OrderTypes.TakeAway, 40_000m, now.AddMinutes(-3));
        var delivery = NewOrder(StoreA, "Website", SystemConstants.OrderStatuses.Delivering,
            SystemConstants.PaymentStatuses.Paid, SystemConstants.OrderTypes.Delivery, 50_000m, now.AddMinutes(-2));
        var otherStorePos = NewOrder(StoreB, "POS", SystemConstants.OrderStatuses.Completed,
            SystemConstants.PaymentStatuses.Paid, SystemConstants.OrderTypes.TakeAway, 99_000m, now.AddMinutes(-1));
        var legacy = NewOrder(StoreA, null, SystemConstants.OrderStatuses.Completed,
            SystemConstants.PaymentStatuses.Paid, SystemConstants.OrderTypes.Delivery, 77_000m, now);

        context.Orders.AddRange(posAwaiting, posPaid, posRefunded, webPending, delivery, otherStorePos, legacy);
        await context.SaveChangesAsync();

        _posAwaitingId = posAwaiting.OrderId;
        _posPaidId = posPaid.OrderId;
        _posRefundedId = posRefunded.OrderId;
        _webPendingId = webPending.OrderId;
        _deliveryId = delivery.OrderId;
        _otherStorePosId = otherStorePos.OrderId;
        _legacyId = legacy.OrderId;

        context.Payments.AddRange(
            NewPayment(posAwaiting.OrderId, 2, SystemConstants.PaymentStatuses.Unpaid, 33_000m, null),
            NewPayment(posPaid.OrderId, 1, SystemConstants.PaymentStatuses.Paid, 33_000m, now.AddMinutes(-5)),
            NewPayment(posRefunded.OrderId, 1, SystemConstants.PaymentStatuses.Refunded, 20_000m, now.AddMinutes(-4)),
            NewPayment(delivery.OrderId, 1, SystemConstants.PaymentStatuses.Paid, 50_000m, now.AddMinutes(-2)),
            NewPayment(otherStorePos.OrderId, 1, SystemConstants.PaymentStatuses.Paid, 99_000m, now.AddMinutes(-1)),
            NewPayment(legacy.OrderId, 3, SystemConstants.PaymentStatuses.Paid, 77_000m, now));
        await context.SaveChangesAsync();
    }

    private static Order NewOrder(
        int storeId,
        string? source,
        int orderStatusId,
        int paymentStatusId,
        int orderTypeId,
        decimal total,
        DateTime createdAt) => new()
    {
        StoreId = storeId,
        Source = source,
        OrderStatusId = orderStatusId,
        PaymentStatusId = paymentStatusId,
        OrderTypeId = orderTypeId,
        SubTotal = total,
        Total = total,
        CreatedAt = createdAt,
        OrderDetails = new List<OrderDetail>(),
        Payments = new List<Payment>()
    };

    private static Payment NewPayment(
        int orderId,
        int methodId,
        int statusId,
        decimal amount,
        DateTime? paidAt) => new()
    {
        OrderId = orderId,
        PaymentMethodId = methodId,
        PaymentStatusId = statusId,
        Amount = amount,
        PaidAt = paidAt
    };

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(ConnectionString)
        .Options);

    private static AdminOrderService CreateService(AppDbContext context) => new(
        context,
        new Mock<IHubContext<OrderHub>>().Object,
        new Mock<IInventoryService>().Object,
        new Mock<IOrderService>().Object);
}
