using CafeChain.Application.Authorization;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Models.Operations;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers;

[RequirePermission(PermissionConstants.PosSessionManage)]
public sealed class AdminPosAccessSessionsController : AdminBaseController
{
    private readonly IPosAccessSessionService _service;
    private readonly IAdminActorContextAccessor _actors;
    private readonly IAdminStoreScopeResolver _scopes;

    public AdminPosAccessSessionsController(
        IPosAccessSessionService service,
        IAdminActorContextAccessor actors,
        IAdminStoreScopeResolver scopes)
    {
        _service = service;
        _actors = actors;
        _scopes = scopes;
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var actor = _actors.Get(User);
        if (actor.StaffId <= 0) return Unauthorized();
        var scope = await _scopes.ResolveAsync(actor, null);
        var stores = scope.IsResolved
            ? scope.AccessibleStores.Select(x => x.StoreId).Distinct().ToArray()
            : Array.Empty<int>();
        var result = await _service.GetActiveAsync(stores, cancellationToken);
        return View(result.Data ?? Array.Empty<PosAccessSessionDto>());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> End(Guid id, string? reason, CancellationToken cancellationToken)
    {
        var actor = _actors.Get(User);
        if (actor.StaffId <= 0) return Unauthorized();
        var session = await _service.GetAsync(id, cancellationToken);
        if (!session.IsSuccess || session.Data == null) return NotFound();
        var scope = await _scopes.ResolveAsync(actor, session.Data.StoreId);
        if (!scope.IsResolved || !scope.AccessibleStores.Any(x => x.StoreId == session.Data.StoreId)) return Forbid();
        var result = await _service.EndAsync(
            id,
            PosAccessSessionStatuses.AdminEnded,
            actor.StaffId,
            string.IsNullOrWhiteSpace(reason) ? "Admin đã kết thúc POS access session." : reason.Trim(),
            cancellationToken);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
