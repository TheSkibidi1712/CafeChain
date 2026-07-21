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
}
