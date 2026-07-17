using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicyConstants.AdminDashboardApp)]
public sealed class DashboardController : Controller
{
    private readonly IDashboardService _service;
    private readonly IAdminActorContextAccessor _actorAccessor;

    public DashboardController(IDashboardService service, IAdminActorContextAccessor actorAccessor)
    {
        _service = service;
        _actorAccessor = actorAccessor;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] DashboardFilterDto filter)
    {
        try
        {
            var page = await _service.GetPageAsync(
                _actorAccessor.Get(User), filter, HttpContext.RequestAborted);
            return View(page);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException exception) { return BadRequest(exception.Message); }
    }

    [HttpGet]
    public IActionResult Guide() => View();

    [HttpGet]
    public async Task<IActionResult> GetSection(
        [FromQuery] DashboardSection section,
        [FromQuery] DashboardFilterDto filter)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Bộ lọc analytics không hợp lệ." });
        return await ExecuteJsonAsync(() => _service.GetSectionAsync(
            _actorAccessor.Get(User), section, filter, HttpContext.RequestAborted));
    }

    // Compatibility adapter: the legacy route now returns the Executive section.
    [HttpGet]
    public Task<IActionResult> GetData([FromQuery] DashboardFilterDto filter) =>
        ExecuteJsonAsync(() => _service.GetSectionAsync(
            _actorAccessor.Get(User), DashboardSection.Executive, filter, HttpContext.RequestAborted));

    // Compatibility adapter for clients that request one of the former analytics widgets.
    [HttpGet]
    public async Task<IActionResult> GetAnalytics(
        [FromQuery] DashboardAnalyticsWidget widget,
        [FromQuery] DashboardAnalyticsFilter filter)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Bộ lọc analytics không hợp lệ." });
        var actor = _actorAccessor.Get(User);
        filter.StaffId = actor.StaffId;
        return await ExecuteJsonAsync(() =>
            _service.GetAnalyticsAsync(widget, filter, HttpContext.RequestAborted));
    }

    private async Task<IActionResult> ExecuteJsonAsync(Func<Task<object>> action)
    {
        try { return Json(new { success = true, data = await action() }); }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { success = false, message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    private Task<IActionResult> ExecuteJsonAsync<T>(Func<Task<T>> action) =>
        ExecuteJsonAsync(async () => (object)await action());
}
