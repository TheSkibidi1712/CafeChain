using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicyConstants.AdminDashboardApp)]
public sealed class AdminDashboardIntelligenceController : Controller
{
    private readonly IDashboardIntelligenceService _service;
    private readonly IAdminActorContextAccessor _actor;

    public AdminDashboardIntelligenceController(IDashboardIntelligenceService service, IAdminActorContextAccessor actor)
    {
        _service = service; _actor = actor;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Parse([FromBody] DashboardPromptRequestDto request) =>
        ExecuteAsync(() => _service.ParseAsync(_actor.Get(User), request, HttpContext.RequestAborted));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Execute([FromBody] DashboardIntentDto intent) =>
        ExecuteAsync(() => _service.ExecuteAsync(_actor.Get(User), intent, HttpContext.RequestAborted));

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Explain([FromBody] Guid analysisId) =>
        ExecuteAsync(() => _service.ExplainAsync(_actor.Get(User), analysisId, HttpContext.RequestAborted));

    private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> action)
    {
        if (!ModelState.IsValid) return UnprocessableEntity(new { success = false, message = "Dữ liệu yêu cầu không hợp lệ." });
        try { return Ok(new { success = true, data = await action(), traceId = HttpContext.TraceIdentifier }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { success = false, message = ex.Message, traceId = HttpContext.TraceIdentifier }); }
        catch (KeyNotFoundException ex) { return NotFound(new { success = false, message = ex.Message, traceId = HttpContext.TraceIdentifier }); }
        catch (ArgumentException ex) { return BadRequest(new { success = false, message = ex.Message, traceId = HttpContext.TraceIdentifier }); }
        catch (InvalidOperationException ex) { return StatusCode(StatusCodes.Status429TooManyRequests, new { success = false, message = ex.Message, traceId = HttpContext.TraceIdentifier }); }
    }
}
