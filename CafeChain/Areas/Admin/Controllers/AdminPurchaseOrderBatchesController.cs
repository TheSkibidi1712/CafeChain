using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Inventories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles =
    RoleConstants.BusinessOwner + "," +
    RoleConstants.AccountantWarehouse + "," +
    RoleConstants.AreaManager + "," +
    RoleConstants.StoreManager + "," +
    RoleConstants.ShiftSupervisor)]
public sealed class AdminPurchaseOrderBatchesController : Controller
{
    private readonly IPurchaseOrderBatchService _service;
    private readonly IAdminActorContextAccessor _actorAccessor;

    public AdminPurchaseOrderBatchesController(IPurchaseOrderBatchService service, IAdminActorContextAccessor actorAccessor)
    {
        _service = service;
        _actorAccessor = actorAccessor;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? status = null, int? supplierId = null)
    {
        var result = await _service.ListAsync(status, supplierId, _actorAccessor.Get(User));
        if (!result.IsSuccess) return Failure(result.ErrorCode, result.Message);
        return View(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var result = await _service.GetDetailAsync(id, _actorAccessor.Get(User));
        if (!result.IsSuccess) return Failure(result.ErrorCode, result.Message);
        return View(result.Data);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePurchaseOrderBatchRequest request)
    {
        var result = await _service.CreateAsync(request, _actorAccessor.Get(User));
        if (!result.IsSuccess)
        {
            TempData["Error"] = $"{result.ErrorCode}: {result.Message}";
            return RedirectToAction("Index", "AdminPurchaseAdviceConsolidation");
        }
        TempData["Success"] = "Đã tạo batch và các PO con theo cửa hàng.";
        return RedirectToAction(nameof(Details), new { id = result.Data!.PurchaseOrderBatchId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, PurchaseOrderBatchTransitionRequest request)
    {
        var result = await _service.ApproveAsync(id, request, _actorAccessor.Get(User));
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Đã duyệt batch và các PO con." : $"{result.ErrorCode}: {result.Message}";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, PurchaseOrderBatchTransitionRequest request)
    {
        var result = await _service.CancelAsync(id, request, _actorAccessor.Get(User));
        TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess ? "Đã hủy batch." : $"{result.ErrorCode}: {result.Message}";
        return RedirectToAction(nameof(Details), new { id });
    }

    private IActionResult Failure(string code, string message)
    {
        if (code == PurchaseOrderBatchErrorCodes.Forbidden) return Forbid();
        if (code == PurchaseOrderBatchErrorCodes.NotFound) return NotFound(message);
        TempData["Error"] = $"{code}: {message}";
        return RedirectToAction(nameof(Index));
    }
}
