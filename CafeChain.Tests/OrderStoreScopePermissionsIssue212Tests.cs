using System.Reflection;
using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Admin.Vouchers;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Services.PayOSIntegration;
using CafeChain.Application.Services.POS;
using CafeChain.Application.Services.Security;
using CafeChain.Controllers.Api.v1;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Infrastructure.Repositories.Admin.POS;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class OrderStoreScopePermissionsIssue212Tests : IntegrationTestBase
{
    private const int ActorStaffId = 21299;
    private const int OriginalStaffId = 21217;
    private const int StoreA = 21203;
    private const int StoreB = 21204;
    private const int WorkShiftId = 21242;

    public static TheoryData<string, string, bool> RoleActionCases => new()
    {
        { RoleConstants.SalesStaff, OrderAccessActions.PosHistory, true },
        { RoleConstants.SalesStaff, OrderAccessActions.Reprint, true },
        { RoleConstants.SalesStaff, OrderAccessActions.AdminList, false },
        { RoleConstants.SalesStaff, OrderAccessActions.RefundRequest, false },
        { RoleConstants.ShiftSupervisor, OrderAccessActions.RefundRequest, true },
        { RoleConstants.ShiftSupervisor, OrderAccessActions.RefundConfirm, false },
        { RoleConstants.AccountantWarehouse, OrderAccessActions.AdminList, true },
        { RoleConstants.AccountantWarehouse, OrderAccessActions.PosHistory, false },
        { RoleConstants.StoreManager, OrderAccessActions.AdminDetail, true },
        { RoleConstants.StoreManager, OrderAccessActions.AdminExport, true },
        { RoleConstants.StoreManager, OrderAccessActions.RefundConfirm, true },
        { "UnknownRole", OrderAccessActions.PosHistory, false }
    };

    [Theory]
    [MemberData(nameof(RoleActionCases))]
    public void ActionPermission_UsesExplicitRoleMatrix(
        string role,
        string action,
        bool expectedAllowed)
    {
        var scope = new Mock<IScopeAuthorizationService>(MockBehavior.Strict);
        var service = new OrderAccessAuthorizationService(scope.Object);
        var actor = Actor(ActorStaffId, StoreA, role);

        var decision = service.AuthorizeAction(actor, action);

        Assert.Equal(
            expectedAllowed ? OrderAccessDecision.Allowed : OrderAccessDecision.Forbidden,
            decision);
    }

    [Fact]
    public async Task StoreAndAreaScopes_AllowOnlyStoresInsideScope()
    {
        using var context = CreateDbContext();
        await SeedStoresAsync(context);
        context.StaffScopes.AddRange(
            new StaffScope
            {
                StaffScopeId = 21201,
                StaffId = 101,
                ScopeTypeId = (int)ScopeLevel.Store,
                ScopeRefId = StoreA
            },
            new StaffScope
            {
                StaffScopeId = 21202,
                StaffId = 102,
                ScopeTypeId = (int)ScopeLevel.Province,
                ScopeRefId = 10
            });
        await context.SaveChangesAsync();

        var service = CreateAccessService(context);
        var storeManager = Actor(101, StoreA, RoleConstants.StoreManager);
        var areaManager = Actor(102, StoreA, RoleConstants.AreaManager);

        Assert.Equal(
            OrderAccessDecision.Allowed,
            await service.AuthorizeAsync(storeManager, OrderAccessActions.AdminList, StoreA));
        Assert.Equal(
            OrderAccessDecision.NotFound,
            await service.AuthorizeAsync(storeManager, OrderAccessActions.AdminDetail, StoreB));
        Assert.Equal(
            OrderAccessDecision.Allowed,
            await service.AuthorizeAsync(areaManager, OrderAccessActions.AdminExport, StoreA));
        Assert.Equal(
            OrderAccessDecision.NotFound,
            await service.AuthorizeAsync(areaManager, OrderAccessActions.Reprint, StoreB));
    }

    [Fact]
    public async Task BusinessOwner_RequiresCountryScope_AndFailsClosedWithoutIt()
    {
        using var context = CreateDbContext();
        await SeedStoresAsync(context);
        context.StaffScopes.Add(new StaffScope
        {
            StaffScopeId = 21203,
            StaffId = 201,
            ScopeTypeId = (int)ScopeLevel.Country,
            ScopeRefId = 1
        });
        await context.SaveChangesAsync();

        var service = CreateAccessService(context);
        var scopedOwner = Actor(201, StoreA, RoleConstants.BusinessOwner);
        var unscopedOwner = Actor(202, StoreA, RoleConstants.BusinessOwner);

        foreach (var action in new[]
                 {
                     OrderAccessActions.AdminList,
                     OrderAccessActions.AdminDetail,
                     OrderAccessActions.AdminExport,
                     OrderAccessActions.Reprint,
                     OrderAccessActions.RefundRequest,
                     OrderAccessActions.RefundConfirm
                 })
        {
            Assert.Equal(
                OrderAccessDecision.Allowed,
                await service.AuthorizeAsync(scopedOwner, action, StoreB));
            Assert.Equal(
                OrderAccessDecision.NotFound,
                await service.AuthorizeAsync(unscopedOwner, action, StoreA));
        }
    }

    [Fact]
    public async Task SystemAdmin_UsesDefaultStaffScopeAndFailsClosedWithoutAccess()
    {
        var scope = new Mock<IScopeAuthorizationService>(MockBehavior.Strict);
        scope
            .Setup(x => x.CanAccessStoreAsync(301, StoreB))
            .ReturnsAsync(false);
        var service = new OrderAccessAuthorizationService(scope.Object);

        var decision = await service.AuthorizeAsync(
            Actor(301, StoreA, RoleConstants.SystemAdmin),
            OrderAccessActions.AdminExport,
            StoreB);

        Assert.Equal(OrderAccessDecision.NotFound, decision);
        scope.Verify(x => x.CanAccessStoreAsync(301, StoreB), Times.Once);
    }

    [Fact]
    public async Task PosHistory_Returns403ForMissingAction_And404ForOutOfScopeStore()
    {
        var orderService = new Mock<IPOSOrderService>(MockBehavior.Strict);
        var inventory = new Mock<IInventoryDeductionService>(MockBehavior.Strict);
        var forbidden = CreatePosController(
            orderService,
            inventory,
            new FixedOrderAccessAuthorizationService(OrderAccessDecision.Forbidden));
        var notFound = CreatePosController(
            orderService,
            inventory,
            new FixedOrderAccessAuthorizationService(OrderAccessDecision.NotFound));

        Assert.IsType<ForbidResult>(await forbidden.GetOrderHistory());
        Assert.IsType<NotFoundResult>(await notFound.GetOrderHistory());
        orderService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reprint_Returns404WhenOrderIsNotInCurrentStore()
    {
        var orderService = new Mock<IPOSOrderService>(MockBehavior.Strict);
        var inventory = new Mock<IInventoryDeductionService>(MockBehavior.Strict);
        orderService
            .Setup(x => x.ReprintOrderAsync(
                500,
                It.IsAny<POSOrderReprintRequestDto>(),
                StoreA))
            .ReturnsAsync(ServiceResult<object>.Failure(
                "Không tìm thấy đơn hàng.",
                errorCode: OrderAccessErrorCodes.NotFound));
        var controller = CreatePosController(
            orderService,
            inventory,
            new FixedOrderAccessAuthorizationService(OrderAccessDecision.Allowed));

        var result = await controller.ReprintOrder(
            500,
            new POSOrderReprintRequestDto { Type = "receipt" });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task OfflineSync_Returns404WhenOriginalWorkShiftIsOutsideActorScope()
    {
        var orderService = new Mock<IPOSOrderService>(MockBehavior.Strict);
        var inventory = new Mock<IInventoryDeductionService>(MockBehavior.Strict);
        orderService
            .Setup(x => x.CommitOfflineSyncedOrderAsync(
                It.IsAny<POSOrderCommitDto>(),
                It.IsAny<OfflineOrderSyncContext>()))
            .ReturnsAsync(ServiceResult<object>.Failure(
                "Không tìm thấy WorkShift gốc.",
                errorCode: OrderAccessErrorCodes.WorkShiftNotFound));
        var controller = CreatePosController(
            orderService,
            inventory,
            new FixedOrderAccessAuthorizationService(OrderAccessDecision.Allowed));

        var result = await controller.SyncOfflineOrders(new OfflineBatchSyncRequestDto
        {
            Orders = new List<OfflineOrderSyncDTO> { CreateOfflineSyncDto() }
        });

        Assert.IsType<NotFoundObjectResult>(result);
        inventory.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RefundController_MapsPermissionTo403_AndCrossScopeTo404()
    {
        var request = new RequestFullOrderRefundDto
        {
            OrderId = 10,
            RefundKey = Guid.NewGuid(),
            Reason = "Khách yêu cầu"
        };
        var service = new Mock<IOrderRefundService>(MockBehavior.Strict);
        service
            .SetupSequence(x => x.RequestFullRefundAsync(
                It.IsAny<RequestFullOrderRefundDto>(),
                It.IsAny<AdminActorContext>()))
            .ReturnsAsync(ServiceResult<OrderRefundResultDto>.Failure(
                "Không có quyền.",
                errorCode: OrderRefundFailureCodes.RoleUnauthorized))
            .ReturnsAsync(ServiceResult<OrderRefundResultDto>.Failure(
                "Không tìm thấy.",
                errorCode: OrderRefundFailureCodes.StoreUnauthorized));
        var controller = WithClaims(new POSOrderRefundController(service.Object));

        Assert.IsType<ForbidResult>(await controller.RequestFullRefund(request));
        Assert.IsType<NotFoundObjectResult>(await controller.RequestFullRefund(request));
    }

    [Fact]
    public void PosOrderAndRefundEndpoints_RequireExactPosPolicy()
    {
        AssertPolicy(
            typeof(POSOrderController).GetMethod(nameof(POSOrderController.GetOrderHistory))!);
        AssertPolicy(
            typeof(POSOrderController).GetMethod(nameof(POSOrderController.ReprintOrder))!);
        AssertPolicy(
            typeof(POSOrderController).GetMethod(nameof(POSOrderController.SyncOfflineOrders))!);
        AssertPolicy(typeof(POSOrderRefundController));
    }

    [Fact]
    public async Task ClientOrderIdLookup_IsAlwaysStoreScoped()
    {
        var clientOrderId = Guid.NewGuid();
        using (var context = CreateDbContext())
        {
            context.Orders.Add(new Order
            {
                OrderId = 21201,
                StoreId = StoreA,
                StaffId = OriginalStaffId,
                ClientOrderId = clientOrderId,
                OrderStatusId = SystemConstants.OrderStatuses.Completed,
                PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                OrderTypeId = SystemConstants.OrderTypes.DineIn,
                Source = "POS",
                SubTotal = 45000m,
                Total = 45000m,
                CreatedAt = DateTime.UtcNow,
                OrderDetails = new List<OrderDetail>(),
                Payments = new List<Payment>()
            });
            await context.SaveChangesAsync();
        }

        using var verifyContext = CreateDbContext();
        var repository = new POSOrderRepository(verifyContext);

        Assert.Null(await repository.FindOrderByClientOrderIdAsync(clientOrderId, StoreB));
        Assert.Equal(
            21201,
            (await repository.FindOrderByClientOrderIdAsync(clientOrderId, StoreA))?.OrderId);
    }

    [Fact]
    public async Task OfflineSync_RejectsForgedStaffOrStoreBeforeClientOrderLookup()
    {
        var harness = CreateOfflineHarness(CreateShift("Closed"));
        var dto = CreateOfflineCommitDto(Guid.NewGuid());

        var result = await harness.Service.CommitOfflineSyncedOrderAsync(
            dto,
            SyncContext(claimedStaffId: 999, claimedStoreId: StoreA));

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderAccessErrorCodes.OfflineAttributionMismatch, result.ErrorCode);
        harness.Repository.Verify(
            x => x.FindOrderByClientOrderIdAsync(It.IsAny<Guid>(), It.IsAny<int>()),
            Times.Never);
        harness.Repository.Verify(
            x => x.CreateOrderAsync(It.IsAny<Order>()),
            Times.Never);
    }

    [Fact]
    public async Task OfflineSync_RejectsWorkShiftOutsideActorScope()
    {
        var harness = CreateOfflineHarness(
            CreateShift("Closed"),
            OrderAccessDecision.NotFound);

        var result = await harness.Service.CommitOfflineSyncedOrderAsync(
            CreateOfflineCommitDto(Guid.NewGuid()),
            SyncContext());

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderAccessErrorCodes.WorkShiftNotFound, result.ErrorCode);
        harness.Repository.Verify(
            x => x.FindOrderByClientOrderIdAsync(It.IsAny<Guid>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task LateOfflineSync_UsesVerifiedWorkShiftAttribution_AndAuditsActorSeparately()
    {
        var shift = CreateShift("Closed");
        shift.ActualEndingCash = 510000m;
        var harness = CreateOfflineHarness(shift);
        var clientOrderId = Guid.NewGuid();
        Order? capturedOrder = null;

        harness.Repository
            .Setup(x => x.FindOrderByClientOrderIdAsync(clientOrderId, StoreA))
            .ReturnsAsync((Order?)null);
        harness.Repository
            .Setup(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .Callback<Order>(order =>
            {
                capturedOrder = order;
                order.OrderId = 21202;
            })
            .ReturnsAsync((Order order) => order);
        harness.Repository
            .Setup(x => x.CreatePaymentAsync(It.IsAny<Payment>()))
            .Returns(Task.CompletedTask);
        harness.Repository.Setup(x => x.BeginTransactionAsync()).Returns(Task.CompletedTask);
        harness.Repository.Setup(x => x.SaveChangesAsync()).Returns(Task.CompletedTask);
        harness.Repository.Setup(x => x.CommitTransactionAsync()).Returns(Task.CompletedTask);

        var result = await harness.Service.CommitOfflineSyncedOrderAsync(
            CreateOfflineCommitDto(clientOrderId),
            SyncContext());

        Assert.True(result.IsSuccess, result.Message);
        Assert.NotNull(capturedOrder);
        Assert.Equal(OriginalStaffId, capturedOrder!.StaffId);
        Assert.Equal(StoreA, capturedOrder.StoreId);
        Assert.Equal(WorkShiftId, capturedOrder.WorkShiftId);
        Assert.Equal(ActorStaffId, harness.Authorization.LastActor?.StaffId);
        Assert.Equal(StoreA, harness.Authorization.LastTargetStoreId);
        Assert.Equal(510000m, shift.ActualEndingCash);
        Assert.True(shift.RequiresReconciliation);
        Assert.True(shift.HasLateOfflineSync);
        Assert.Equal(1, shift.LateOfflineSyncCount);
        harness.Print.VerifyNoOtherCalls();
    }

    private static OrderAccessAuthorizationService CreateAccessService(Data.AppDbContext context) =>
        new(new ScopeAuthorizationService(context));

    private static AdminActorContext Actor(int staffId, int storeId, params string[] roles) =>
        new()
        {
            StaffId = staffId,
            StoreId = storeId,
            RoleNames = roles
        };

    private static async Task SeedStoresAsync(Data.AppDbContext context)
    {
        context.Stores.AddRange(
            new Store
            {
                StoreId = StoreA,
                Name = "Store A",
                Address = "A",
                Phone = "1",
                ProvinceId = 10,
                Active = true,
                CreatedAt = DateTime.UtcNow
            },
            new Store
            {
                StoreId = StoreB,
                Name = "Store B",
                Address = "B",
                Phone = "2",
                ProvinceId = 11,
                Active = true,
                CreatedAt = DateTime.UtcNow
            });
        context.Accounts.AddRange(
            ActiveAccount(101),
            ActiveAccount(102),
            ActiveAccount(201),
            ActiveAccount(202));
        context.Staffs.AddRange(
            ActiveStaff(101, StoreA),
            ActiveStaff(102, StoreA),
            ActiveStaff(201, StoreA),
            ActiveStaff(202, StoreA));
        await context.SaveChangesAsync();
    }

    private static Account ActiveAccount(int id) => new()
    {
        AccountId = id,
        Email = $"scope-{id}@test.local",
        PasswordHash = "test",
        Active = true,
        CreatedAt = DateTime.UtcNow
    };

    private static Staff ActiveStaff(int id, int storeId) => new()
    {
        StaffId = id,
        AccountId = id,
        StoreId = storeId,
        FullName = $"Scope actor {id}",
        EmployeeStatus = 2,
        Active = true,
        CreatedAt = DateTime.UtcNow
    };

    private static POSOrderController CreatePosController(
        Mock<IPOSOrderService> orderService,
        Mock<IInventoryDeductionService> inventory,
        IOrderAccessAuthorizationService authorization)
    {
        var controller = new POSOrderController(
            orderService.Object,
            inventory.Object,
            Mock.Of<ILogger<POSOrderController>>(),
            authorization);
        return WithClaims(controller);
    }

    private static T WithClaims<T>(T controller) where T : ControllerBase
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("StaffId", ActorStaffId.ToString()),
            new Claim("StoreId", StoreA.ToString()),
            new Claim(ClaimTypes.Role, RoleConstants.SalesStaff)
        }, "Issue212");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
        return controller;
    }

    private static void AssertPolicy(MemberInfo member)
    {
        var policies = member.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Select(attribute => attribute.Policy)
            .ToArray();
        Assert.Contains(AuthorizationPolicyConstants.PosApp, policies);
    }

    private static OfflineHarness CreateOfflineHarness(
        WorkShift shift,
        OrderAccessDecision decision = OrderAccessDecision.Allowed)
    {
        var repository = new Mock<IPOSOrderRepository>(MockBehavior.Loose);
        var workShiftService = new Mock<IWorkShiftService>(MockBehavior.Strict);
        var validator = new Mock<IPOSStoreMenuSaleValidator>(MockBehavior.Strict);
        var print = new Mock<IPrintDispatcher>(MockBehavior.Strict);
        var authorization = new CapturingOrderAccessAuthorizationService(decision);

        workShiftService
            .Setup(x => x.GetShiftByIdAsync(WorkShiftId))
            .ReturnsAsync(shift);
        validator
            .Setup(x => x.ValidateOfflineAsync(
                It.IsAny<POSOrderItemDto>(),
                StoreA,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<POSAcceptedSaleLineDto>.Success(new POSAcceptedSaleLineDto
            {
                StoreMenuItemId = 100,
                DrinkSizeId = 200,
                DrinkId = 10,
                SizeId = 2,
                DrinkName = "Americano",
                SizeName = "M",
                AcceptedBasePrice = 45000m,
                AcceptedUnitPrice = 45000m,
                PriceSource = StoreMenuPriceSources.Global,
                CatalogVersion = 1
            }));

        var service = new POSOrderService(
            repository.Object,
            workShiftService.Object,
            Mock.Of<IAdminVoucherService>(),
            print.Object,
            Mock.Of<IPayOSService>(),
            Mock.Of<ILogger<POSOrderService>>(),
            validator.Object,
            null,
            authorization);

        return new OfflineHarness(service, repository, print, authorization);
    }

    private static POSOrderCommitDto CreateOfflineCommitDto(Guid clientOrderId) =>
        new()
        {
            ClientOrderId = clientOrderId,
            Items = new List<POSOrderItemDto>
            {
                new()
                {
                    DrinkId = 10,
                    SizeId = 2,
                    StoreMenuItemId = 100,
                    DrinkSizeId = 200,
                    AcceptedBasePrice = 45000m,
                    AcceptedUnitPrice = 45000m,
                    PriceSource = StoreMenuPriceSources.Global,
                    CatalogVersion = 1,
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
            OrderTypeId = SystemConstants.OrderTypes.DineIn,
            SkipPrint = true
        };

    private static OfflineOrderSyncDTO CreateOfflineSyncDto() =>
        new()
        {
            ClientOrderId = Guid.NewGuid(),
            LocalId = "issue-212-local",
            StoreId = StoreA,
            StaffId = OriginalStaffId,
            WorkShiftId = WorkShiftId,
            SoldAt = DateTime.UtcNow.AddMinutes(-10),
            PaymentMethodId = 1,
            TotalAmount = 45000m,
            ReceivedAmount = 50000m,
            ChangeAmount = 5000m,
            OrderTypeId = SystemConstants.OrderTypes.DineIn,
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

    private static OfflineOrderSyncContext SyncContext(
        int claimedStaffId = OriginalStaffId,
        int claimedStoreId = StoreA) =>
        new()
        {
            ActorStaffId = ActorStaffId,
            ActorRoleNames = new[] { RoleConstants.SalesStaff },
            ClaimedStaffId = claimedStaffId,
            ClaimedStoreId = claimedStoreId,
            WorkShiftId = WorkShiftId,
            SoldAt = DateTime.UtcNow.AddMinutes(-10)
        };

    private static WorkShift CreateShift(string status) =>
        new()
        {
            ShiftId = WorkShiftId,
            StoreId = StoreA,
            UserId = OriginalStaffId,
            Status = status,
            StartingCash = 500000m,
            ExpectedEndingCash = 500000m
        };

    private sealed record OfflineHarness(
        POSOrderService Service,
        Mock<IPOSOrderRepository> Repository,
        Mock<IPrintDispatcher> Print,
        CapturingOrderAccessAuthorizationService Authorization);

    private sealed class FixedOrderAccessAuthorizationService : IOrderAccessAuthorizationService
    {
        private readonly OrderAccessDecision _decision;

        public FixedOrderAccessAuthorizationService(OrderAccessDecision decision)
        {
            _decision = decision;
        }

        public OrderAccessDecision AuthorizeAction(AdminActorContext actor, string action) =>
            _decision;

        public Task<OrderAccessDecision> AuthorizeAsync(
            AdminActorContext actor,
            string action,
            int targetStoreId) =>
            Task.FromResult(_decision);
    }

    private sealed class CapturingOrderAccessAuthorizationService : IOrderAccessAuthorizationService
    {
        private readonly OrderAccessDecision _decision;

        public CapturingOrderAccessAuthorizationService(OrderAccessDecision decision)
        {
            _decision = decision;
        }

        public AdminActorContext? LastActor { get; private set; }
        public int? LastTargetStoreId { get; private set; }

        public OrderAccessDecision AuthorizeAction(AdminActorContext actor, string action) =>
            _decision;

        public Task<OrderAccessDecision> AuthorizeAsync(
            AdminActorContext actor,
            string action,
            int targetStoreId)
        {
            LastActor = actor;
            LastTargetStoreId = targetStoreId;
            return Task.FromResult(_decision);
        }
    }
}
