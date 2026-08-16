using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.Dashboard;
using CafeChain.Application.Interfaces.AppLauncher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Controllers;

[Authorize]
public sealed class AppLauncherController : Controller
{
    private readonly IAppLauncherService _service;
    private readonly IPosLaunchCoordinator _posLaunchCoordinator;
    private readonly IAdminActorContextAccessor _actorContext;
    private readonly IDashboardAuthorizationService _dashboardAuthorization;

    public AppLauncherController(
        IAppLauncherService service,
        IPosLaunchCoordinator posLaunchCoordinator,
        IAdminActorContextAccessor actorContext,
        IDashboardAuthorizationService dashboardAuthorization)
    {
        _service = service;
        _posLaunchCoordinator = posLaunchCoordinator;
        _actorContext = actorContext;
        _dashboardAuthorization = dashboardAuthorization;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var accountValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(accountValue, out var accountId) || accountId <= 0)
            return Challenge();

        var model = await _service.GetAppsAsync(
            accountId,
            User.FindFirstValue(ClaimTypes.Name),
            HttpContext.RequestAborted);
        return View(model);
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicyConstants.AdminPanelAccess)]
    public async Task<IActionResult> OpenAdminDashboard()
    {
        try
        {
            await _dashboardAuthorization.GetAccessAsync(
                _actorContext.Get(User),
                HttpContext.RequestAborted);
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }
        catch (UnauthorizedAccessException)
        {
            return RedirectToAction("MyProfile", "AdminProfile", new { area = "Admin" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
    public async Task<IActionResult> LaunchPos()
    {
        if (!TryGetStoreId(out var storeId))
            return Unauthorized(new { state = "Failed", errorCode = "POS_STORE_CLAIM_MISSING", message = "Phiên đăng nhập thiếu StoreId." });

        var result = await _posLaunchCoordinator.EnsureReadyAsync(storeId, HttpContext.RequestAborted);
        return result.IsReady ? Ok(result) : BadRequest(result);
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
    public async Task<IActionResult> PosStatus()
    {
        if (!TryGetStoreId(out var storeId))
            return Unauthorized(new { state = "Failed", errorCode = "POS_STORE_CLAIM_MISSING", message = "Phiên đăng nhập thiếu StoreId." });

        return Ok(await _posLaunchCoordinator.GetStatusAsync(storeId, HttpContext.RequestAborted));
    }

    private bool TryGetStoreId(out int storeId) =>
        int.TryParse(User.FindFirstValue("StoreId"), out storeId) && storeId > 0;
}
