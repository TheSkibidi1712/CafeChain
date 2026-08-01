using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.DTOs.Admin.StockAlerts;
using CafeChain.Application.DTOs.Admin.StoreScope;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.Actor;
using CafeChain.Application.Services.Admin.StoreScope;
using CafeChain.Application.Services.Security;
using CafeChain.Areas.Admin.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CafeChain.Tests;

public sealed class AdminStoreScopeResolutionTests : IntegrationTestBase
{
    [Fact]
    public async Task StoreResolver_UsesExplicitAuthorizedStore()
    {
        await using var db = CreateDbContext();
        var selected = new SelectedStoreContextStub();
        var resolver = CreateResolver(db, selected);

        var result = await resolver.ResolveAsync(AccountantActor(), 1);

        Assert.True(result.IsResolved);
        Assert.Equal(1, result.StoreId);
        Assert.Equal(AdminStoreScopeResolutionSource.ExplicitRequest, result.Source);
        Assert.Equal(1, selected.StoreId);
    }

    [Fact]
    public async Task StoreResolver_ExplicitUnauthorizedStoreReturnsForbidden()
    {
        await using var db = CreateDbContext();
        var resolver = CreateResolver(db, new SelectedStoreContextStub());

        var result = await resolver.ResolveAsync(AccountantActor(), 2);

        Assert.Equal(AdminStoreScopeResolutionStatus.RequestedStoreForbidden, result.Status);
        Assert.Equal(AdminStoreScopeErrorCodes.StoreScopeForbidden, result.ErrorCode);
    }

    [Fact]
    public async Task StoreResolver_ExplicitMissingStoreReturnsNotFound()
    {
        await using var db = CreateDbContext();
        var resolver = CreateResolver(db, new SelectedStoreContextStub());

        var result = await resolver.ResolveAsync(AccountantActor(), 999999);

        Assert.Equal(AdminStoreScopeResolutionStatus.StoreNotFound, result.Status);
        Assert.Equal(AdminStoreScopeErrorCodes.StoreNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task StoreResolver_UsesSelectedSessionStore()
    {
        await using var db = CreateDbContext();
        var selected = new SelectedStoreContextStub { StoreId = 1 };
        var resolver = CreateResolver(db, selected);

        var result = await resolver.ResolveAsync(AccountantActor());

        Assert.Equal(1, result.StoreId);
        Assert.Equal(AdminStoreScopeResolutionSource.SelectedSessionStore, result.Source);
    }

    [Fact]
    public async Task StoreResolver_FallsBackToStaffStore()
    {
        await using var db = CreateDbContext();
        var resolver = CreateResolver(db, new SelectedStoreContextStub());

        var result = await resolver.ResolveAsync(AccountantActor());

        Assert.Equal(1, result.StoreId);
        Assert.Equal(AdminStoreScopeResolutionSource.StaffStore, result.Source);
    }

    [Fact]
    public async Task StoreResolver_ClearsInaccessibleSelectedStoreAndFallsBack()
    {
        await using var db = CreateDbContext();
        var selected = new SelectedStoreContextStub { StoreId = 2 };
        var resolver = CreateResolver(db, selected);

        var result = await resolver.ResolveAsync(AccountantActor());

        Assert.Equal(1, result.StoreId);
        Assert.Equal(AdminStoreScopeResolutionSource.StaffStore, result.Source);
        Assert.Equal(AdminStoreScopeErrorCodes.SelectedStoreNoLongerAccessible, result.WarningCode);
        Assert.Equal(1, selected.StoreId);
    }

    [Fact]
    public async Task StoreResolver_FallsBackToFirstScopedStore()
    {
        await using var db = CreateDbContext();
        var staff = await db.Staffs.SingleAsync(x => x.StaffId == 5);
        staff.StoreId = 2;
        await db.SaveChangesAsync();
        var resolver = CreateResolver(db, new SelectedStoreContextStub());

        var result = await resolver.ResolveAsync(AccountantActor());

        Assert.Equal(1, result.StoreId);
        Assert.Equal(AdminStoreScopeResolutionSource.FirstAccessibleStore, result.Source);
    }

    [Fact]
    public async Task StoreResolver_NoScopeReturnsConfigurationError()
    {
        await using var db = CreateDbContext();
        db.StaffScopes.RemoveRange(db.StaffScopes.Where(x => x.StaffId == 5));
        await db.SaveChangesAsync();
        var resolver = CreateResolver(db, new SelectedStoreContextStub());

        var result = await resolver.ResolveAsync(AccountantActor());

        Assert.Equal(AdminStoreScopeResolutionStatus.NoAccessibleStore, result.Status);
        Assert.Equal(AdminStoreScopeErrorCodes.StoreScopeNotConfigured, result.ErrorCode);
    }

    [Fact]
    public async Task StoreResolver_ExplicitForbiddenDoesNotFallback()
    {
        await using var db = CreateDbContext();
        var selected = new SelectedStoreContextStub { StoreId = 1 };
        var resolver = CreateResolver(db, selected);

        var result = await resolver.ResolveAsync(AccountantActor(), 2);

        Assert.False(result.IsResolved);
        Assert.Equal(AdminStoreScopeResolutionStatus.RequestedStoreForbidden, result.Status);
        Assert.Equal(1, selected.StoreId);
    }

    [Fact]
    public async Task RestockRequests_NoStoreId_UsesResolvedStore()
    {
        await using var db = CreateDbContext();
        var restock = new Mock<IRestockRequestService>();
        restock.Setup(x => x.ListForStoreAsync(1, "SUBMITTED", 1, 20))
            .ReturnsAsync(ServiceResult<RestockRequestListResultDto>.Success(
                new RestockRequestListResultDto { StoreId = 1, Page = 1, PageSize = 20 }));
        var controller = new AdminRestockRequestsController(
            restock.Object,
            Mock.Of<IRestockRequestWorkflowService>(),
            new AdminActorContextAccessor(),
            CreateResolver(db, new SelectedStoreContextStub()));
        AttachUser(controller, RoleConstants.AccountantWarehouse, 5, 1, PermissionConstants.RestockView);

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
        restock.Verify(x => x.ListForStoreAsync(1, "SUBMITTED", 1, 20), Times.Once);
    }

    [Fact]
    public async Task StockAlerts_NoStoreId_UsesResolvedStore()
    {
        await using var db = CreateDbContext();
        var alerts = new Mock<IStockAlertManagerService>();
        alerts.Setup(x => x.ListForStoreAsync(1, "OPEN", 1, 20))
            .ReturnsAsync(ServiceResult<StockAlertListResultDto>.Success(
                new StockAlertListResultDto { StoreId = 1, Page = 1, PageSize = 20 }));
        var controller = new AdminStockAlertsController(
            alerts.Object,
            Mock.Of<IRestockRequestService>(),
            new AdminActorContextAccessor(),
            CreateResolver(db, new SelectedStoreContextStub()));
        AttachUser(controller, RoleConstants.AccountantWarehouse, 5, 1, PermissionConstants.StockAlertView);

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
        alerts.Verify(x => x.ListForStoreAsync(1, "OPEN", 1, 20), Times.Once);
    }

    [Fact]
    public async Task BranchReceipts_NoStoreId_UsesResolvedStore()
    {
        await using var db = CreateDbContext();
        var receipts = new Mock<IBranchReceiptService>();
        receipts.Setup(x => x.ListForStoreAsync(
                1,
                5,
                1,
                It.IsAny<IReadOnlyCollection<string>>(),
                null))
            .ReturnsAsync(ServiceResult<List<BranchReceiptListItemDto>>.Success(new()));
        var controller = new AdminBranchReceiptsController(
            receipts.Object,
            new AdminActorContextAccessor(),
            CreateResolver(db, new SelectedStoreContextStub()),
            db);
        AttachUser(controller, RoleConstants.AccountantWarehouse, 5, 1, PermissionConstants.ReceiptView);

        var result = await controller.Index();

        Assert.IsType<ViewResult>(result);
        receipts.Verify(x => x.ListForStoreAsync(
            1,
            5,
            1,
            It.IsAny<IReadOnlyCollection<string>>(),
            null), Times.Once);
    }

    [Fact]
    public async Task AccountantWarehouse_StockAlertMutationStillRejected()
    {
        await using var db = CreateDbContext();
        var alerts = new Mock<IStockAlertManagerService>();
        var controller = new AdminStockAlertsController(
            alerts.Object,
            Mock.Of<IRestockRequestService>(),
            new AdminActorContextAccessor(),
            CreateResolver(db, new SelectedStoreContextStub()));
        AttachUser(controller, RoleConstants.AccountantWarehouse, 5, 1, PermissionConstants.StockAlertView);

        var result = await controller.Confirm(1, "note", null, 1);

        Assert.IsType<RedirectToActionResult>(result);
        alerts.Verify(x => x.ConfirmAsync(
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task StoreManager_OtherStoreRejected()
    {
        await using var db = CreateDbContext();
        var alerts = new Mock<IStockAlertManagerService>();
        var controller = new AdminStockAlertsController(
            alerts.Object,
            Mock.Of<IRestockRequestService>(),
            new AdminActorContextAccessor(),
            CreateResolver(db, new SelectedStoreContextStub()));
        AttachUser(controller, RoleConstants.StoreManager, 3, 1, PermissionConstants.StockAlertView);

        var result = await controller.Index(storeId: 2);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, controller.Response.StatusCode);
        Assert.Equal("~/Areas/Admin/Views/Shared/StoreScopeError.cshtml", view.ViewName);
        alerts.Verify(x => x.ListForStoreAsync(
            It.IsAny<int>(),
            It.IsAny<string?>(),
            It.IsAny<int>(),
            It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task AreaManager_OutsideAreaRejected()
    {
        await using var db = CreateDbContext();
        var store1 = await db.Stores.SingleAsync(x => x.StoreId == 1);
        var store2 = await db.Stores.SingleAsync(x => x.StoreId == 2);
        store1.ProvinceId = 79;
        store2.ProvinceId = 80;
        await db.SaveChangesAsync();
        var restock = new Mock<IRestockRequestService>();
        var controller = new AdminRestockRequestsController(
            restock.Object,
            Mock.Of<IRestockRequestWorkflowService>(),
            new AdminActorContextAccessor(),
            CreateResolver(db, new SelectedStoreContextStub()));
        AttachUser(controller, RoleConstants.AreaManager, 2, 1, PermissionConstants.RestockView);

        var result = await controller.Index(storeId: 2);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, controller.Response.StatusCode);
        restock.Verify(x => x.ListForStoreAsync(
            It.IsAny<int>(),
            It.IsAny<string?>(),
            It.IsAny<int>(),
            It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task StandardScope_SystemAdminWithoutStaffScopeHasNoStores()
    {
        await using var db = CreateDbContext();
        db.StaffScopes.RemoveRange(db.StaffScopes.Where(x => x.StaffId == 6));
        await db.SaveChangesAsync();
        var scope = new ScopeAuthorizationService(db);
        var allowed = await scope.GetAllowedStoresAsync(6);

        Assert.Empty(allowed);
        Assert.False(await scope.CanAccessStoreAsync(6, 1));
    }

    [Fact]
    public async Task ReorderSuggestionScope_SystemAdminGetsAllActiveStoresOnly()
    {
        await using var db = CreateDbContext();
        db.StaffScopes.RemoveRange(db.StaffScopes.Where(x => x.StaffId == 6));
        await db.SaveChangesAsync();
        var resolver = CreateResolver(db, new SelectedStoreContextStub());
        var activeStoreIds = await db.Stores
            .Where(x => x.Active)
            .Select(x => x.StoreId)
            .OrderBy(x => x)
            .ToListAsync();

        var result = await resolver.ResolveAsync(
            new AdminActorContext
            {
                AccountId = 6,
                StaffId = 6,
                RoleNames = [RoleConstants.SystemAdmin]
            },
            null,
            AdminStoreScopeMode.ReorderSuggestion);

        Assert.True(result.IsResolved);
        Assert.Equal(activeStoreIds, result.AccessibleStores.Select(x => x.StoreId).OrderBy(x => x));
    }

    [Fact]
    public async Task InactiveSystemAdmin_DoesNotReceiveGlobalScopeByRole()
    {
        await using var db = CreateDbContext();
        db.StaffScopes.RemoveRange(db.StaffScopes.Where(x => x.StaffId == 6));
        var staff = await db.Staffs.SingleAsync(x => x.StaffId == 6);
        staff.Active = false;
        await db.SaveChangesAsync();
        var scope = new ScopeAuthorizationService(db);

        Assert.Empty(await scope.GetAllowedStoresAsync(6));
        Assert.False(await scope.CanAccessStoreAsync(6, 1));
    }

    private static AdminActorContext AccountantActor() => new()
    {
        StaffId = 5,
        StoreId = 1,
        RoleNames = new[] { RoleConstants.AccountantWarehouse }
    };

    private static AdminStoreScopeResolver CreateResolver(
        Data.AppDbContext db,
        IAdminSelectedStoreContext selectedStoreContext) =>
        new(db, new ScopeAuthorizationService(db), selectedStoreContext);

    private static void AttachUser(
        Controller controller,
        string role,
        int staffId,
        int storeId,
        params string[] allowedPermissions)
    {
        var allowed = allowedPermissions.ToHashSet(StringComparer.Ordinal);
        var permissionService = new Mock<IAdminPermissionService>();
        permissionService
            .Setup(x => x.HasPermissionAsync(staffId, It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync((int _, string code, int? _) =>
                ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
                {
                    AccountId = staffId,
                    PermissionCode = code,
                    Allowed = allowed.Contains(code)
                }));
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, staffId.ToString()),
            new Claim("StaffId", staffId.ToString()),
            new Claim("StoreId", storeId.ToString()),
            new Claim(ClaimTypes.Role, role)
        }, "Test"));
        var httpContext = new DefaultHttpContext
        {
            User = user,
            RequestServices = new ServiceCollection()
                .AddSingleton(permissionService.Object)
                .BuildServiceProvider()
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        controller.Url = Mock.Of<IUrlHelper>();
    }

    private sealed class SelectedStoreContextStub : IAdminSelectedStoreContext
    {
        public int? StoreId { get; set; }
        public int? GetSelectedStoreId() => StoreId;
        public void SetSelectedStoreId(int storeId) => StoreId = storeId;
        public void ClearSelectedStoreId() => StoreId = null;
    }
}
