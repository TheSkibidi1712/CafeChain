using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.Dashboard;
using CafeChain.Application.Interfaces.AppLauncher;
using CafeChain.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CafeChain.Tests;

public sealed class AppLauncherControllerTests
{
    private static readonly AdminActorContext Actor = new() { AccountId = 42, StaffId = 24 };

    [Fact]
    public async Task Open_admin_dashboard_redirects_to_dashboard_when_full_access_is_available()
    {
        var dashboard = new Mock<IDashboardAuthorizationService>();
        dashboard.Setup(service => service.GetAccessAsync(Actor, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DashboardAuthorizationDto());

        var result = await CreateController(dashboard.Object).OpenAdminDashboard();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Dashboard", redirect.ControllerName);
        Assert.Equal("Admin", redirect.RouteValues?["area"]);
    }

    [Fact]
    public async Task Open_admin_dashboard_redirects_to_profile_when_full_access_is_denied()
    {
        var dashboard = new Mock<IDashboardAuthorizationService>();
        dashboard.Setup(service => service.GetAccessAsync(Actor, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Dashboard access denied."));

        var result = await CreateController(dashboard.Object).OpenAdminDashboard();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("MyProfile", redirect.ActionName);
        Assert.Equal("AdminProfile", redirect.ControllerName);
        Assert.Equal("Admin", redirect.RouteValues?["area"]);
    }

    [Fact]
    public void Open_admin_dashboard_requires_admin_panel_access()
    {
        var authorization = typeof(AppLauncherController)
            .GetMethod(nameof(AppLauncherController.OpenAdminDashboard))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(AuthorizationPolicyConstants.AdminPanelAccess, authorization.Policy);
    }

    private static AppLauncherController CreateController(IDashboardAuthorizationService dashboard)
    {
        var actorAccessor = new Mock<IAdminActorContextAccessor>();
        actorAccessor.Setup(accessor => accessor.Get(It.IsAny<ClaimsPrincipal>())).Returns(Actor);
        return new AppLauncherController(
            Mock.Of<IAppLauncherService>(),
            Mock.Of<IPosLaunchCoordinator>(),
            actorAccessor.Object,
            dashboard)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }
}
