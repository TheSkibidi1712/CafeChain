using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.DTOs.AppLauncher;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Results;
using CafeChain.Application.Services.AppLauncher;
using Moq;

namespace CafeChain.Tests;

public sealed class AppLauncherServiceTests
{
    [Fact]
    public async Task Returns_only_cards_allowed_by_effective_permission()
    {
        var permissions = new Mock<IAdminPermissionService>();
        permissions.Setup(x => x.HasPermissionAsync(42, It.IsAny<string>(), null))
            .ReturnsAsync((int _, string code, int? _) =>
                ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
                {
                    AccountId = 42,
                    PermissionCode = code,
                    Allowed = code is PermissionConstants.AppStaffHub or PermissionConstants.AppPos
                }));
        var service = new AppLauncherService(permissions.Object);

        var result = await service.GetAppsAsync(42, "Lan");

        Assert.Equal("Lan", result.DisplayName);
        Assert.Equal([AppCode.StaffHub, AppCode.Pos], result.Apps.Select(x => x.Code));
        Assert.DoesNotContain(result.Apps, x => x.Code == AppCode.AdminDashboard);
        var pos = Assert.Single(result.Apps, x => x.Code == AppCode.Pos);
        Assert.True(pos.RequiresLaunch);
        Assert.Equal("#", pos.Route);
        Assert.DoesNotContain("PrintBridge", pos.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task No_permissions_returns_empty_state_without_redirect_decision()
    {
        var permissions = new Mock<IAdminPermissionService>();
        permissions.Setup(x => x.HasPermissionAsync(7, It.IsAny<string>(), null))
            .ReturnsAsync(ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
            {
                AccountId = 7,
                Allowed = false
            }));
        var service = new AppLauncherService(permissions.Object);

        var result = await service.GetAppsAsync(7, null);

        Assert.False(result.HasAvailableApps);
        Assert.Empty(result.Apps);
    }

    [Fact]
    public async Task Operational_ice_permission_adds_direct_card_without_admin_dashboard_access()
    {
        var permissions = new Mock<IAdminPermissionService>();
        permissions.Setup(x => x.HasPermissionAsync(17, It.IsAny<string>(), null))
            .ReturnsAsync((int _, string code, int? _) =>
                ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
                {
                    AccountId = 17,
                    PermissionCode = code,
                    Allowed = code is PermissionConstants.OperationalIceView or PermissionConstants.AppStaffHub
                }));
        var service = new AppLauncherService(permissions.Object);

        var result = await service.GetAppsAsync(17, "Ca trưởng");

        var operationalIce = Assert.Single(result.Apps, x => x.Code == AppCode.OperationalIce);
        Assert.Equal("/Admin/AdminOperationalIce", operationalIce.Route);
        Assert.False(operationalIce.RequiresLaunch);
        Assert.DoesNotContain(result.Apps, x => x.Code == AppCode.AdminDashboard);
    }

    [Fact]
    public async Task Admin_dashboard_suppresses_operational_ice_fallback_card()
    {
        var permissions = new Mock<IAdminPermissionService>();
        permissions.Setup(x => x.HasPermissionAsync(18, It.IsAny<string>(), null))
            .ReturnsAsync((int _, string code, int? _) =>
                ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
                {
                    AccountId = 18,
                    PermissionCode = code,
                    Allowed = code is PermissionConstants.AppAdminDashboard or PermissionConstants.OperationalIceView
                }));
        var service = new AppLauncherService(permissions.Object);

        var result = await service.GetAppsAsync(18, "Quản lý");

        Assert.Contains(result.Apps, x => x.Code == AppCode.AdminDashboard);
        Assert.DoesNotContain(result.Apps, x => x.Code == AppCode.OperationalIce);
    }
}
