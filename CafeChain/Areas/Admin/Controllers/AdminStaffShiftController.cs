using System.Security.Claims;
using CafeChain.Application.Authorization;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Staffs;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Results;
using CafeChain.ViewModels.Admin.Staffs;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers;

[RequirePermission(PermissionConstants.ShiftView)]
public sealed class AdminStaffShiftController : AdminBaseController
{
    private readonly IAdminStaffShiftService _service;
    private readonly IAdminActorContextAccessor _actorAccessor;
    private readonly IAdminStoreScopeResolver _scopeResolver;
    private readonly IAdminPermissionService _permissionService;

    public AdminStaffShiftController(
        IAdminStaffShiftService service,
        IAdminActorContextAccessor actorAccessor,
        IAdminStoreScopeResolver scopeResolver,
        IAdminPermissionService permissionService)
    {
        _service = service;
        _actorAccessor = actorAccessor;
        _scopeResolver = scopeResolver;
        _permissionService = permissionService;
    }

    public async Task<IActionResult> Index(DateTime? startDate, int? targetStoreId, CancellationToken ct)
    {
        var actor = _actorAccessor.Get(User);
        var scope = await _scopeResolver.ResolveAsync(actor, targetStoreId, ct);
        if (!scope.IsResolved) return StoreScopeFailure(scope);

        var date = (startDate ?? DateTime.Today).Date;
        var offset = (7 + date.DayOfWeek - DayOfWeek.Monday) % 7;
        var weekStart = date.AddDays(-offset);
        var accountId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : 0;
        var permissionResult = await _permissionService.GetEffectivePermissionCodesAsync(accountId);
        var permissions = permissionResult.Data ?? new HashSet<string>();
        var stores = scope.AccessibleStores.Select(x => new StaffShiftStoreOptionVM(x.StoreId, x.StoreName)).ToList();
        var model = await _service.GetPageAsync(scope.StoreId!.Value, weekStart, weekStart.AddDays(6), stores, permissions, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, RequirePermission(PermissionConstants.ShiftCreate)]
    public async Task<IActionResult> AssignShift(int targetStoreId, AssignStaffShiftRequest request, CancellationToken ct) =>
        await ExecuteAsync(targetStoreId, (storeId, actorId) => _service.AssignAsync(storeId, actorId, request, ct), ct);

    [HttpPost, ValidateAntiForgeryToken, RequirePermission(PermissionConstants.ShiftUpdate)]
    public async Task<IActionResult> UpdateStaffShift(int targetStoreId, UpdateStaffShiftRequest request, CancellationToken ct) =>
        await ExecuteAsync(targetStoreId, (storeId, actorId) => _service.UpdateAssignmentAsync(storeId, actorId, request, ct), ct);

    [HttpPost, ValidateAntiForgeryToken, RequirePermission(PermissionConstants.ShiftCancel)]
    public async Task<IActionResult> CancelStaffShift(int targetStoreId, CancelStaffShiftRequest request, CancellationToken ct) =>
        await ExecuteAsync(targetStoreId, (storeId, actorId) => _service.CancelAsync(storeId, actorId, request, ct), ct);

    [HttpPost, ValidateAntiForgeryToken, RequirePermission(PermissionConstants.ShiftCreate)]
    public async Task<IActionResult> CreateShift(int targetStoreId, CreateShiftTemplateRequest request, CancellationToken ct) =>
        await ExecuteAsync(targetStoreId, (storeId, actorId) => _service.CreateTemplateAsync(storeId, actorId, request, ct), ct);

    [HttpPost, ValidateAntiForgeryToken, RequirePermission(PermissionConstants.ShiftUpdate)]
    public async Task<IActionResult> UpdateShift(int targetStoreId, UpdateShiftTemplateRequest request, CancellationToken ct) =>
        await ExecuteAsync(targetStoreId, (storeId, actorId) => _service.UpdateTemplateAsync(storeId, actorId, request, ct), ct);

    [HttpPost, ValidateAntiForgeryToken, RequirePermission(PermissionConstants.ShiftUpdate)]
    public async Task<IActionResult> ToggleShift(int targetStoreId, ToggleShiftTemplateRequest request, CancellationToken ct) =>
        await ExecuteAsync(targetStoreId, (storeId, actorId) => _service.ToggleTemplateAsync(storeId, actorId, request, ct), ct);

    private async Task<IActionResult> ExecuteAsync(
        int targetStoreId,
        Func<int, int, Task<ServiceResult>> operation,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Dữ liệu gửi lên không hợp lệ." });

        var actor = _actorAccessor.Get(User);
        var scope = await _scopeResolver.ResolveAsync(actor, targetStoreId, ct);
        if (!scope.IsResolved) return StatusCode(StatusCodes.Status403Forbidden,
            new { success = false, message = scope.Message, errorCode = scope.ErrorCode });

        var result = await operation(scope.StoreId!.Value, actor.StaffId);
        if (result.IsSuccess) return Ok(new { success = true, message = result.Message, entityId = result.EntityId });
        var status = result.ErrorCode switch
        {
            "FORBIDDEN" => StatusCodes.Status403Forbidden,
            "NOT_FOUND" => StatusCodes.Status404NotFound,
            "CONCURRENCY_CONFLICT" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return StatusCode(status, new { success = false, message = result.Message, errorCode = result.ErrorCode });
    }
}
