using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.Dashboard;
using CafeChain.Models.Stores;
using Moq;

namespace CafeChain.Tests;

public sealed class DashboardWidgetAuthorizationTests
{
    private static readonly AdminActorContext Actor = new() { AccountId = 11, StaffId = 7 };

    [Fact]
    public async Task SectionPermission_DoesNotGrantWidgetPermission()
    {
        var service = CreateService([
            PermissionConstants.AppAdminDashboard,
            PermissionConstants.DashboardInventoryView,
            "Inventory.View"
        ]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.AuthorizeWidgetsAsync(Actor, [DashboardAnalyticsWidget.InventoryShortageRisk]));
    }

    [Fact]
    public async Task WidgetPermission_DoesNotBypassDomainPermission()
    {
        var service = CreateService([
            PermissionConstants.AppAdminDashboard,
            PermissionConstants.DashboardInventoryView,
            PermissionConstants.DashboardWidgetInventoryShortageRiskView
        ]);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.AuthorizeWidgetsAsync(Actor, [DashboardAnalyticsWidget.InventoryShortageRisk]));
    }

    [Fact]
    public async Task Access_PrunesSectionsWithoutAnAuthorizedWidget()
    {
        var service = CreateService([
            PermissionConstants.AppAdminDashboard,
            PermissionConstants.DashboardOperationsView,
            PermissionConstants.PosWorkShiftView,
            PermissionConstants.DashboardInventoryView,
            "Inventory.View",
            PermissionConstants.DashboardWidgetWorkShiftSalesView
        ]);

        var access = await service.GetAccessAsync(Actor);

        Assert.Equal([DashboardSection.Operations], access.AllowedSections);
        Assert.Equal([DashboardAnalyticsWidget.WorkShiftSales], access.AllowedWidgets);
    }

    private static DashboardAuthorizationService CreateService(IEnumerable<string> permissionCodes)
    {
        var permissions = new Mock<IAdminPermissionService>();
        permissions.Setup(x => x.GetEffectivePermissionCodesAsync(Actor.AccountId))
            .ReturnsAsync(ServiceResult<HashSet<string>>.Success(
                new HashSet<string>(permissionCodes, StringComparer.Ordinal)));

        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.GetAllowedStoresAsync(Actor.StaffId))
            .ReturnsAsync([new Store { StoreId = 3, Name = "Cửa hàng 3", Active = true }]);

        return new DashboardAuthorizationService(permissions.Object, scope.Object);
    }
}
