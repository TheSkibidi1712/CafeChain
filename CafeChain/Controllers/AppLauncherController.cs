using System.Security.Claims;
using CafeChain.Application.Interfaces.AppLauncher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Controllers;

[Authorize]
public sealed class AppLauncherController : Controller
{
    private readonly IAppLauncherService _service;

    public AppLauncherController(IAppLauncherService service)
    {
        _service = service;
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
}
