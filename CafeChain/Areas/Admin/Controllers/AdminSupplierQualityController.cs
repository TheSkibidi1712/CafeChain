using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Areas.Admin.Controllers;

[Authorize(Roles = RoleConstants.BusinessOwner + "," + RoleConstants.AccountantWarehouse + ","
    + RoleConstants.AreaManager + "," + RoleConstants.StoreManager)]
public sealed class AdminSupplierQualityController : AdminBaseController
{
    private readonly ISupplierQualityService _service;
    private readonly IAdminActorContextAccessor _actor;
    private readonly IAdminStoreScopeResolver _storeScopeResolver;
    private readonly AppDbContext _context;

    public AdminSupplierQualityController(
        ISupplierQualityService service,
        IAdminActorContextAccessor actor,
        IAdminStoreScopeResolver storeScopeResolver,
        AppDbContext context)
    {
        _service = service;
        _actor = actor;
        _storeScopeResolver = storeScopeResolver;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int? storeId = null,
        int? supplierId = null,
        int days = 90,
        DateTime? from = null,
        DateTime? to = null)
    {
        var actor = _actor.Get(User);
        if (actor.StaffId <= 0)
            return Unauthorized();
        var storeScope = await _storeScopeResolver.ResolveAsync(actor, storeId);
        if (!storeScope.IsResolved)
            return StoreScopeFailure(storeScope);
        var selectedStoreId = storeScope.StoreId!.Value;
        var toUtc = to.HasValue ? ToUtcBoundary(to.Value.Date.AddDays(1)) : DateTime.UtcNow;
        var fromUtc = from.HasValue
            ? ToUtcBoundary(from.Value.Date)
            : toUtc.AddDays(-NormalizeDays(days));
        var result = await _service.GetDashboardAsync(
            selectedStoreId, supplierId, fromUtc, toUtc, actor.StaffId, actor.RoleNames);
        if (!result.IsSuccess || result.Data == null) return Forbid();
        SetStoreScopeViewData(storeScope);
        ViewBag.Stores = storeScope.AccessibleStores;
        ViewBag.Suppliers = await _context.Suppliers.AsNoTracking()
            .Where(x => x.Active && x.SupplierStores.Any(s => s.StoreId == selectedStoreId && s.Active))
            .OrderBy(x => x.Name)
            .ToListAsync();
        ViewBag.Days = days;
        ViewBag.CustomFrom = from?.Date;
        ViewBag.CustomTo = to?.Date;
        return View(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int branchReceiptLineId)
    {
        var actor = _actor.Get(User);
        var contextResult = await _service.GetReceiptContextAsync(
            branchReceiptLineId, actor.StaffId, actor.RoleNames);
        if (!contextResult.IsSuccess || contextResult.Data == null)
        {
            TempData["ErrorMessage"] = contextResult.Message;
            return RedirectToAction(nameof(Index));
        }
        return View(new CreateSupplierReceiptIssuePageDto
        {
            Context = contextResult.Data,
            Input = new CreateSupplierReceiptIssueRequest
            {
                BranchReceiptLineId = branchReceiptLineId,
                IssueType = contextResult.Data.SuggestedIssueType ?? SupplierReceiptIssueTypes.Other,
                AffectedBaseQuantity = contextResult.Data.RejectedBaseQuantity
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSupplierReceiptIssueRequest input)
    {
        var actor = _actor.Get(User);
        var result = await _service.CreateIssueAsync(input, actor.StaffId, actor.RoleNames);
        if (!result.IsSuccess || result.Data == null)
        {
            TempData["ErrorMessage"] = result.Message;
            var contextResult = await _service.GetReceiptContextAsync(
                input.BranchReceiptLineId, actor.StaffId, actor.RoleNames);
            if (!contextResult.IsSuccess || contextResult.Data == null)
                return RedirectToAction(nameof(Index));
            return View(new CreateSupplierReceiptIssuePageDto
            {
                Context = contextResult.Data,
                Input = input
            });
        }
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Index), new
        {
            storeId = result.Data.StoreId,
            supplierId = result.Data.SupplierId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transition(int id, SupplierReceiptIssueTransitionRequest input)
    {
        var actor = _actor.Get(User);
        var result = await _service.TransitionAsync(id, input, actor.StaffId, actor.RoleNames);
        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(Index), result.Data == null
            ? null
            : new { storeId = result.Data.StoreId, supplierId = result.Data.SupplierId });
    }

    private static int NormalizeDays(int days) => days is 30 or 90 or 180 ? days : 90;

    private static DateTime ToUtcBoundary(DateTime localDate) =>
        DateTime.SpecifyKind(localDate, DateTimeKind.Local).ToUniversalTime();
}
