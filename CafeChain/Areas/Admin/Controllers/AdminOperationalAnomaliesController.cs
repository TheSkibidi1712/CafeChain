using CafeChain.Application.Authorization;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Areas.Admin.Controllers;

[RequirePermission(PermissionConstants.AppAdminDashboard)]
public sealed class AdminOperationalAnomaliesController : AdminBaseController
{
    private readonly IAnomalyDetectionService _service;
    private readonly IAdminActorContextAccessor _actor;
    private readonly IAIService _ai;
    private readonly IAdminStoreScopeResolver _scope;
    public AdminOperationalAnomaliesController(IAnomalyDetectionService service, IAdminActorContextAccessor actor, IAIService ai, IAdminStoreScopeResolver scope)
    { _service = service; _actor = actor; _ai = ai; _scope = scope; }

    [HttpGet]
    public async Task<IActionResult> Index(int? targetStoreId, CancellationToken ct)
    {
        var actor = _actor.Get(User); var scope = await _scope.ResolveAsync(actor, targetStoreId, ct);
        if (!scope.IsResolved) return StoreScopeFailure(scope);
        ViewBag.Stores = scope.AccessibleStores; ViewBag.SelectedStoreId = scope.StoreId;
        return View(await _service.GetOpenAsync(actor, scope.StoreId!.Value, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Open(int storeId, CancellationToken ct)
    {
        try { return Ok(new { success = true, data = await _service.GetOpenAsync(_actor.Get(User), storeId, ct), traceId = HttpContext.TraceIdentifier }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Feedback([FromBody] AnomalyFeedbackDto input, CancellationToken ct)
    {
        try { await _service.RecordFeedbackAsync(_actor.Get(User), input, ct); return Ok(new { success = true, traceId = HttpContext.TraceIdentifier }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { success = false, message = ex.Message }); }
        catch (DbUpdateConcurrencyException ex) { return Conflict(new { success = false, message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { success = false, message = ex.Message }); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Explain([FromBody] int anomalyId, CancellationToken ct)
    {
        try
        {
            var context = await _service.GetExplanationContextAsync(_actor.Get(User), anomalyId, ct);
            return Ok(new { success = true, data = await _ai.ExplainAnomalyAsync(context, ct), traceId = HttpContext.TraceIdentifier });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { success = false, message = ex.Message }); }
    }
}
