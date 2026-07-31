using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.StoreScope;
using CafeChain.Application.Interfaces.Admin;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Interfaces;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Policies.Orders;
using CafeChain.Application.Services.Admin;
using CafeChain.Application.Services.Admin.StoreScope;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Hubs;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class OrderStoreFilterContractIssue212Tests
{
    private static readonly AdminStoreOptionDto StoreA = new()
    {
        StoreId = 1,
        StoreName = "Store A"
    };

    private static readonly AdminStoreOptionDto StoreB = new()
    {
        StoreId = 2,
        StoreName = "Store B"
    };

    [Fact]
    public async Task AuthorizedStores_ReturnsOnlyAllowedStores()
    {
        var harness = CreateHarness(
            Actor(RoleConstants.AreaManager),
            ResolvedScope(StoreA, StoreB));

        var result = await harness.Controller.GetAuthorizedStores();

        var ok = Assert.IsType<OkObjectResult>(result);
        var stores = Assert.IsAssignableFrom<IReadOnlyList<AdminStoreOptionDto>>(ok.Value);
        Assert.Equal(new[] { 1, 2 }, stores.Select(x => x.StoreId));
        harness.Service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AreaManager_CanViewOneAssignedStore()
    {
        var harness = CreateHarness(
            Actor(RoleConstants.AreaManager),
            ResolvedScopeFor(2, StoreA, StoreB),
            requestedStoreId: 2);
        SetupHistoryPage(harness.Service, new[] { 2 });

        var result = await harness.Controller.GetOrderHistoryData(
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            storeId: 2);

        Assert.IsType<OkObjectResult>(result);
        harness.Resolver.Verify(
            x => x.ResolveAsync(
                It.IsAny<AdminActorContext>(),
                2,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AreaManager_CanViewAllStoresWithinScope()
    {
        var harness = CreateHarness(
            Actor(RoleConstants.AreaManager),
            ResolvedScope(StoreA, StoreB));
        SetupHistoryPage(harness.Service, new[] { 1, 2 });

        var result = await harness.Controller.GetOrderHistoryData(
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            storeId: null,
            allWithinScope: true);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task AreaManager_CannotViewOutsideArea()
    {
        var harness = CreateHarness(
            Actor(RoleConstants.AreaManager),
            ForbiddenScope(StoreA, StoreB),
            requestedStoreId: 3);

        var result = await harness.Controller.GetOrderHistoryData(
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            storeId: 3);

        Assert.IsType<NotFoundObjectResult>(result);
        harness.Service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BusinessOwner_OneStoreAndAllStores_UseCountryScopedResolution()
    {
        var actor = Actor(RoleConstants.BusinessOwner);
        var oneStore = CreateHarness(actor, ResolvedScope(StoreA, StoreB), requestedStoreId: 1);
        SetupHistoryPage(oneStore.Service, new[] { 1 });
        Assert.IsType<OkObjectResult>(await oneStore.Controller.GetOrderHistoryData(
            "", "", "", null, null, 1));

        var allStores = CreateHarness(actor, ResolvedScope(StoreA, StoreB));
        SetupHistoryPage(allStores.Service, new[] { 1, 2 });
        Assert.IsType<OkObjectResult>(await allStores.Controller.GetOrderHistoryData(
            "", "", "", null, null, null, true));
    }

    [Fact]
    public async Task BusinessOwner_WithoutCountryScope_FailsClosed()
    {
        var harness = CreateHarness(
            Actor(RoleConstants.BusinessOwner),
            NoAccessibleStoreScope());

        var result = await harness.Controller.GetOrderHistoryData(
            "", "", "", null, null, null, true);

        Assert.IsType<ForbidResult>(result);
        harness.Service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StoreManager_CannotSwitchOutsideManagedStore()
    {
        var harness = CreateHarness(
            Actor(RoleConstants.StoreManager),
            ForbiddenScope(StoreA),
            requestedStoreId: 2);

        var result = await harness.Controller.GetOrderHistoryData(
            "", "", "", null, null, 2);

        Assert.IsType<NotFoundObjectResult>(result);
        harness.Service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Export_AndKpi_UseTheSameValidatedStoreFilter()
    {
        var rows = new List<AdminOrderHistoryRowDto>
        {
            new()
            {
                OrderId = 10,
                StoreId = 1,
                Total = 25_000m,
                OrderStatusId = SystemConstants.PaymentStatuses.Paid,
                CreatedAt = DateTime.UtcNow
            }
        };
        var harness = CreateHarness(
            Actor(RoleConstants.AreaManager),
            ResolvedScope(StoreA, StoreB));
        SetupHistoryPage(harness.Service, new[] { 1, 2 }, rows);
        harness.Service
            .Setup(x => x.GetFilteredOrdersForExportAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.Is<IReadOnlyCollection<int>>(ids => ids.Order().SequenceEqual(new[] { 1, 2 }))))
            .ReturnsAsync(rows);

        Assert.IsType<OkObjectResult>(await harness.Controller.GetOrderHistoryData(
            "", "", "", null, null, null, true));
        Assert.IsType<FileContentResult>(await harness.Controller.ExportCSV(
            "", "", "", null, null, null, true));

        harness.Service.Verify(
            x => x.GetFilteredOrdersForExportAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.Is<IReadOnlyCollection<int>>(ids => ids.Order().SequenceEqual(new[] { 1, 2 }))),
            Times.Once);
        harness.Service.Verify(
            x => x.GetPosSalesHistoryAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.Is<IReadOnlyCollection<int>>(ids =>
                    ids.Order().SequenceEqual(new[] { 1, 2 }))),
            Times.Once);
    }

    [Fact]
    public async Task Detail_RequiresTheRowsValidatedStore()
    {
        var harness = CreateHarness(
            Actor(RoleConstants.AreaManager),
            ResolvedScopeFor(2, StoreA, StoreB),
            requestedStoreId: 2);
        harness.Service
            .Setup(x => x.GetOrderHistoryDetailAsync(50, 2))
            .ReturnsAsync(new AdminOrderHistoryDetailDto { OrderId = 50 });

        Assert.IsType<OkObjectResult>(
            await harness.Controller.GetHistoryDetail(50, storeId: 2));
        harness.Service.Verify(x => x.GetOrderHistoryDetailAsync(50, 2), Times.Once);

        var selectedStoreHarness = CreateHarness(
            Actor(RoleConstants.StoreManager),
            ResolvedScope(StoreA));
        selectedStoreHarness.Service
            .Setup(x => x.GetOrderHistoryDetailAsync(51, 1))
            .ReturnsAsync(new AdminOrderHistoryDetailDto { OrderId = 51 });

        Assert.IsType<OkObjectResult>(
            await selectedStoreHarness.Controller.GetHistoryDetail(51, storeId: null));
        selectedStoreHarness.Service.Verify(
            x => x.GetOrderHistoryDetailAsync(51, 1),
            Times.Once);
    }

    private static Harness CreateHarness(
        AdminActorContext actor,
        AdminStoreScopeResolution resolution,
        int? requestedStoreId = null)
    {
        var service = new Mock<IAdminOrderService>(MockBehavior.Strict);
        var actorAccessor = new Mock<IAdminActorContextAccessor>(MockBehavior.Strict);
        actorAccessor
            .Setup(x => x.Get(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .Returns(actor);

        var resolver = new Mock<IAdminStoreScopeResolver>(MockBehavior.Strict);
        resolver
            .Setup(x => x.ResolveAsync(
                actor,
                requestedStoreId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolution);

        var authorization = new Mock<IOrderAccessAuthorizationService>(MockBehavior.Strict);
        authorization
            .Setup(x => x.AuthorizeAction(actor, It.IsAny<string>()))
            .Returns(OrderAccessDecision.Allowed);

        var controller = new AdminOrderController(
            service.Object,
            actorAccessor.Object,
            resolver.Object,
            authorization.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return new Harness(controller, service, resolver);
    }

    private static void SetupHistoryPage(
        Mock<IAdminOrderService> service,
        IReadOnlyCollection<int> expectedStoreIds,
        List<AdminOrderHistoryRowDto>? rows = null)
    {
        service
            .Setup(x => x.GetPosSalesHistoryAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.Is<IReadOnlyCollection<int>>(ids =>
                    ids.Order().SequenceEqual(expectedStoreIds.Order()))))
            .ReturnsAsync(new AdminOrderHistoryPageDto
            {
                Page = 1,
                PageSize = 20,
                TotalItems = rows?.Count ?? 0,
                TotalPages = rows?.Count > 0 ? 1 : 0,
                Items = rows ?? new List<AdminOrderHistoryRowDto>()
            });
    }

    private static AdminActorContext Actor(string role) =>
        new()
        {
            StaffId = 99,
            StoreId = 1,
            RoleNames = new[] { role }
        };

    private static AdminStoreScopeResolution ResolvedScope(
        params AdminStoreOptionDto[] stores) =>
        ResolvedScopeFor(stores[0].StoreId, stores);

    private static AdminStoreScopeResolution ResolvedScopeFor(
        int selectedStoreId,
        params AdminStoreOptionDto[] stores) =>
        new()
        {
            Status = AdminStoreScopeResolutionStatus.Resolved,
            StoreId = selectedStoreId,
            AccessibleStores = stores
        };

    private static AdminStoreScopeResolution ForbiddenScope(
        params AdminStoreOptionDto[] stores) =>
        new()
        {
            Status = AdminStoreScopeResolutionStatus.RequestedStoreForbidden,
            ErrorCode = AdminStoreScopeErrorCodes.StoreScopeForbidden,
            AccessibleStores = stores
        };

    private static AdminStoreScopeResolution NoAccessibleStoreScope() =>
        new()
        {
            Status = AdminStoreScopeResolutionStatus.NoAccessibleStore,
            ErrorCode = AdminStoreScopeErrorCodes.StoreScopeNotConfigured
        };

    private sealed record Harness(
        AdminOrderController Controller,
        Mock<IAdminOrderService> Service,
        Mock<IAdminStoreScopeResolver> Resolver);
}

public sealed class OrderStoreFilterQueryIssue212Tests : IntegrationTestBase
{
    [Fact]
    public async Task SystemAdmin_DefaultResolverDoesNotBypassStaffScope()
    {
        await using var context = CreateDbContext();
        context.Stores.AddRange(
            new Store
            {
                StoreId = 21201,
                Name = "Store A",
                Address = "A",
                Phone = "1",
                Active = true,
                CreatedAt = DateTime.UtcNow
            },
            new Store
            {
                StoreId = 21202,
                Name = "Store B",
                Address = "B",
                Phone = "2",
                Active = true,
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();
        var scopeAuthorization = new Mock<IScopeAuthorizationService>(MockBehavior.Strict);
        scopeAuthorization
            .Setup(x => x.GetAllowedStoresAsync(99, StoreScopePurpose.Default))
            .ReturnsAsync(new List<Store>());
        var selectedStore = new Mock<IAdminSelectedStoreContext>(MockBehavior.Strict);
        selectedStore.Setup(x => x.GetSelectedStoreId()).Returns((int?)null);
        var resolver = new AdminStoreScopeResolver(
            context,
            scopeAuthorization.Object,
            selectedStore.Object);

        var result = await resolver.ResolveAsync(new AdminActorContext
        {
            StaffId = 99,
            StoreId = 21201,
            RoleNames = new[] { RoleConstants.SystemAdmin }
        });

        Assert.Equal(AdminStoreScopeResolutionStatus.NoAccessibleStore, result.Status);
        scopeAuthorization.Verify(
            x => x.GetAllowedStoresAsync(99, StoreScopePurpose.Default),
            Times.Once);
    }

    [Fact]
    public async Task HistoryPagination_UsesAllAndOnlyValidatedStoreIds()
    {
        await using var context = CreateDbContext();
        var storeA = AddPaidOrder(context, 1, 10_000m, DateTime.UtcNow.AddMinutes(-3));
        var storeB = AddPaidOrder(context, 2, 20_000m, DateTime.UtcNow.AddMinutes(-2));
        var outOfScope = AddPaidOrder(context, 3, 30_000m, DateTime.UtcNow.AddMinutes(-1));
        await context.SaveChangesAsync();
        AddPaidPayment(context, storeA);
        AddPaidPayment(context, storeB);
        AddPaidPayment(context, outOfScope);
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetOrderHistoryAsync(
            new DataTablesRequest
            {
                Draw = 9,
                Start = 0,
                Length = 10,
                Order = new() { new() { Column = 0, Dir = "asc" } },
                Columns = new() { new() { Data = "orderId" } }
            },
            new[] { 1, 2 });

        Assert.Equal(2, result.RecordsTotal);
        Assert.Equal(2, result.RecordsFiltered);
        Assert.Equal(new[] { 1, 2 }, result.Data.Select(x => x.StoreId).Order());
        Assert.DoesNotContain(result.Data, x => x.OrderId == outOfScope.OrderId);
    }

    [Fact]
    public async Task Export_OneStoreAndAllScope_ReturnMatchingRows()
    {
        await using var context = CreateDbContext();
        var storeA = AddPaidOrder(context, 1, 10_000m, DateTime.UtcNow.AddMinutes(-2));
        var storeB = AddPaidOrder(context, 2, 20_000m, DateTime.UtcNow.AddMinutes(-1));
        await context.SaveChangesAsync();
        AddPaidPayment(context, storeA);
        AddPaidPayment(context, storeB);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var oneStore = await service.GetFilteredOrdersForExportAsync(
            "", "", "", null, null, new[] { 1 });
        var allScope = await service.GetFilteredOrdersForExportAsync(
            "", "", "", null, null, new[] { 1, 2 });

        Assert.Equal(storeA.OrderId, Assert.Single(oneStore).OrderId);
        Assert.Equal(
            new[] { storeA.OrderId, storeB.OrderId }.Order(),
            allScope.Select(x => x.OrderId).Order());
    }

    private static Order AddPaidOrder(
        CafeChain.Data.AppDbContext context,
        int storeId,
        decimal total,
        DateTime createdAt)
    {
        var order = new Order
        {
            StoreId = storeId,
            Source = OrderSources.Pos,
            OrderStatusId = SystemConstants.OrderStatuses.Completed,
            PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
            OrderTypeId = SystemConstants.OrderTypes.DineIn,
            SubTotal = total,
            Total = total,
            CreatedAt = createdAt,
            OrderDetails = new List<OrderDetail>(),
            Payments = new List<Payment>()
        };
        context.Orders.Add(order);
        return order;
    }

    private static void AddPaidPayment(
        CafeChain.Data.AppDbContext context,
        Order order)
    {
        context.Payments.Add(new Payment
        {
            OrderId = order.OrderId,
            Amount = order.Total,
            PaymentMethodId = 1,
            PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
            PaidAt = order.CreatedAt
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
