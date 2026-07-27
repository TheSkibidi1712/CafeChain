using CafeChain.Application.Authorization;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Staffs;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.Staffs;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Areas.Admin.Controllers;

[RequirePermission(PermissionConstants.ShiftView)]
public sealed class AdminShiftOptimizationController : AdminBaseController
{
    private readonly IShiftOptimizationService _service;
    private readonly IAdminActorContextAccessor _actor;
    private readonly IAdminStoreScopeResolver _scope;

    public AdminShiftOptimizationController(
        IShiftOptimizationService service,
        IAdminActorContextAccessor actor,
        IAdminStoreScopeResolver scope)
    {
        _service = service;
        _actor = actor;
        _scope = scope;
    }

    public async Task<IActionResult> Index(int? targetStoreId, CancellationToken ct)
    {
        var actor = _actor.Get(User);
        var scope = await _scope.ResolveAsync(actor, targetStoreId, ct);
        if (!scope.IsResolved)
            return StoreScopeFailure(scope);

        ViewBag.Stores = scope.AccessibleStores;
        ViewBag.StoreName = scope.AccessibleStores
            .FirstOrDefault(x => x.StoreId == scope.StoreId.Value)?.StoreName
            ?? string.Empty;
        return View(await _service.GetSetupAsync(actor, scope.StoreId!.Value, ct));
    }

    [HttpPost, ValidateAntiForgeryToken, RequirePermission(PermissionConstants.ShiftCreate)]
    public Task<IActionResult> SaveAvailability([FromBody] SaveAvailabilityRuleDto input) =>
        Execute(async () =>
        {
            await _service.SaveAvailabilityAsync(
                _actor.Get(User), input, HttpContext.RequestAborted);
            return new { saved = true };
        });

    [HttpPost, ValidateAntiForgeryToken, RequirePermission(PermissionConstants.ShiftCreate)]
    public Task<IActionResult> SaveConstraint([FromBody] SaveWorkConstraintDto input) =>
        Execute(async () =>
        {
            await _service.SaveConstraintAsync(
                _actor.Get(User), input, HttpContext.RequestAborted);
            return new { saved = true };
        });

    [HttpPost, ValidateAntiForgeryToken, RequirePermission(PermissionConstants.ShiftCreate)]
    public Task<IActionResult> SaveRequirement([FromBody] SaveStaffingRequirementDto input) =>
        Execute(async () =>
        {
            await _service.SaveRequirementAsync(
                _actor.Get(User), input, HttpContext.RequestAborted);
            return new { saved = true };
        });

    [HttpPost, ValidateAntiForgeryToken, RequirePermission(PermissionConstants.ShiftCreate)]
    public Task<IActionResult> SaveTimeOff([FromBody] SaveTimeOffDto input) =>
        Execute(async () =>
        {
            await _service.SaveTimeOffAsync(
                _actor.Get(User), input, HttpContext.RequestAborted);
            return new { saved = true };
        });

    private async Task<IActionResult> Execute<T>(Func<Task<T>> operation)
    {
        if (!ModelState.IsValid)
            return UnprocessableEntity(new
            {
                success = false,
                message = "Dữ liệu không hợp lệ."
            });

        try
        {
            return Ok(new
            {
                success = true,
                data = await operation(),
                traceId = HttpContext.TraceIdentifier
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { success = false, message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return Conflict(new { success = false, message = ex.Message });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}
