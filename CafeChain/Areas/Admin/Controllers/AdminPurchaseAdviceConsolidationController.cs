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
    RoleConstants.AreaManager + "," +
    RoleConstants.StoreManager + "," +
    RoleConstants.AccountantWarehouse)]
public sealed class AdminPurchaseAdviceConsolidationController : Controller
{
    private readonly IPurchaseAdviceConsolidationService _service;
    private readonly IAdminActorContextAccessor _actorAccessor;

    public AdminPurchaseAdviceConsolidationController(
        IPurchaseAdviceConsolidationService service,
        IAdminActorContextAccessor actorAccessor)
    {
        _service = service;
        _actorAccessor = actorAccessor;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int? storeId = null,
        int? areaId = null,
        string? status = null,
        DateTime? neededByDate = null,
        int? ingredientId = null,
        string? priority = null,
        int? supplierId = null)
    {
        var result = await _service.GetQueueAsync(new PurchaseAdviceConsolidationFilterDto
        {
            StoreId = storeId,
            AreaId = areaId,
            Status = status,
            NeededByDate = neededByDate,
            IngredientId = ingredientId,
            Priority = priority,
            SupplierId = supplierId
        }, _actorAccessor.Get(User));
        if (!result.IsSuccess) return Failure(result.ErrorCode, result.Message);
        return View(result.Data);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(
        PurchaseAdviceConsolidationPreviewRequest request,
        int[] selectedLineIds)
    {
        var submittedSelections = request.Lines
            .Where(x => selectedLineIds.Contains(x.PurchaseAdviceLineId))
            .GroupBy(x => x.PurchaseAdviceLineId)
            .ToDictionary(x => x.Key, x => x.First());
        request.Lines = request.Lines
            .Where(x => selectedLineIds.Contains(x.PurchaseAdviceLineId))
            .ToList();
        var preview = await _service.PreviewAsync(request, _actorAccessor.Get(User));
        var page = await _service.GetQueueAsync(new PurchaseAdviceConsolidationFilterDto
        {
            SupplierId = request.SupplierId > 0 ? request.SupplierId : null
        }, _actorAccessor.Get(User));
        if (!page.IsSuccess) return Failure(page.ErrorCode, page.Message);
        ViewBag.Preview = preview.Data;
        ViewBag.PreviewError = preview.IsSuccess ? null : $"{preview.ErrorCode}: {preview.Message}";
        ViewBag.SelectedLineIds = selectedLineIds;
        ViewBag.SelectedSupplierId = request.SupplierId;
        ViewBag.SubmittedSelections = submittedSelections;
        return View("Index", page.Data);
    }

    private IActionResult Failure(string code, string message)
    {
        if (code is PurchaseAdviceErrorCodes.Forbidden or PurchaseAdviceErrorCodes.StoreScopeMismatch)
            return Forbid();
        TempData["Error"] = $"{code}: {message}";
        return RedirectToAction(nameof(Index));
    }
}
