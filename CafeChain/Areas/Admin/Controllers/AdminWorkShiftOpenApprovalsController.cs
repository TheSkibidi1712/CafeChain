using CafeChain.Application.Authorization;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Interfaces.POS;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers;

[RequirePermission(PermissionConstants.PosWorkShiftApproveLateOpen)]
public sealed class AdminWorkShiftOpenApprovalsController : AdminBaseController
{
    private readonly IWorkShiftOpenApprovalService _service;
    private readonly IAdminActorContextAccessor _actorAccessor;
    private readonly IAdminStoreScopeResolver _scopeResolver;

    public AdminWorkShiftOpenApprovalsController(
        IWorkShiftOpenApprovalService service,
        IAdminActorContextAccessor actorAccessor,
        IAdminStoreScopeResolver scopeResolver)
    {
        _service = service;
        _actorAccessor = actorAccessor;
        _scopeResolver = scopeResolver;
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var actor = _actorAccessor.Get(User);
        if (actor.StaffId <= 0) return Unauthorized();
        var scope = await _scopeResolver.ResolveAsync(actor, null);
        var storeIds = scope.IsResolved
            ? scope.AccessibleStores.Select(x => x.StoreId).Distinct().ToArray()
            : Array.Empty<int>();
        var result = await _service.GetPendingAsync(actor.StaffId, storeIds, cancellationToken);
        return View(result.Data ?? Array.Empty<WorkShiftOpenApprovalDto>());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(
        Guid id,
        int storeId,
        [FromForm] DecideWorkShiftOpenApprovalRequestDto request,
        CancellationToken cancellationToken)
    {
        var actor = _actorAccessor.Get(User);
        if (actor.StaffId <= 0) return Unauthorized();
        var result = await _service.DecideAsync(
            actor.StaffId, storeId, id, request, cancellationToken);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
