using CafeChain.Application.Constants;
using CafeChain.Application.Authorization;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Data;
using CafeChain.Models.Inventories.Auditing;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Areas.Admin.Controllers
{
    // Admin Purchase Orders Controller - Printable PO View Updated (No Signatures, Added Addresses & GIAO TẠI Section)
    [RequirePermission(PermissionConstants.PurchaseOrderView)]
    public sealed class AdminPurchaseOrdersController : AdminStoreScopedController
    {
        private readonly IPurchaseOrderService _service;
        private readonly IRestockAllocationService _allocations;
        private readonly IUnitConversionService _conversion;
        private readonly IAdminActorContextAccessor _actor;
        private readonly AppDbContext _context;
        private readonly IAdminStoreScopeResolver _storeScopeResolver;
        private readonly ISupplierIntelligenceService? _supplierIntelligence;

        public AdminPurchaseOrdersController(
            IPurchaseOrderService service,
            IRestockAllocationService allocations,
            IUnitConversionService conversion,
            IAdminActorContextAccessor actor,
            AppDbContext context,
            IAdminStoreScopeResolver storeScopeResolver,
            ISupplierIntelligenceService? supplierIntelligence = null)
        {
            _service = service;
            _allocations = allocations;
            _conversion = conversion;
            _actor = actor;
            _context = context;
            _storeScopeResolver = storeScopeResolver;
            _supplierIntelligence = supplierIntelligence;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? storeId = null, string? status = null)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.PurchaseOrderView)) return Forbid();
            var actor = _actor.Get(User);
            if (actor.StaffId <= 0) return Unauthorized();
            var storeScope = await _storeScopeResolver.ResolveAsync(actor, storeId);
            if (!storeScope.IsResolved) return StoreScopeFailure(storeScope);
            SetStoreScopeViewData(storeScope);
            ViewBag.StatusFilter = status;
            ViewBag.CanCreate = await HasEffectivePermissionAsync(PermissionConstants.PurchaseOrderCreate);
            return View(await _service.ListAsync(storeScope.StoreId, status, actor.StaffId, actor.RoleNames));
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.PurchaseOrderView)) return Forbid();
            var actor = _actor.Get(User);
            var result = await _service.GetDetailAsync(id, actor.StaffId, actor.RoleNames);
            if (!result.IsSuccess || result.Data == null) return Forbid();
            ViewBag.CanApprove = actor.StaffId != result.Data.CreatedByStaffId
                && await HasEffectivePermissionAsync(PermissionConstants.PurchaseOrderApprove);
            ViewBag.CanSend = await HasEffectivePermissionAsync(PermissionConstants.PurchaseOrderSend);
            ViewBag.CanCancel = await HasEffectivePermissionAsync(PermissionConstants.PurchaseOrderCancel);
            ViewBag.CanReceive = await HasEffectivePermissionAsync(PermissionConstants.ReceiptCreate);
            ViewBag.CanCloseRemaining = await HasEffectivePermissionAsync(PermissionConstants.PurchaseOrderCloseRemaining);
            return View(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> Print(int id)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.PurchaseOrderView)) return Forbid();
            var actor = _actor.Get(User);
            var result = await _service.GetDetailAsync(id, actor.StaffId, actor.RoleNames);
            if (!result.IsSuccess || result.Data == null) return NotFound();
            return View("Print", result.Data);
        }

        [HttpGet]
        [RequirePermission(PermissionConstants.PurchaseOrderCreate)]
        public async Task<IActionResult> Create(int? restockRequestId = null)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.PurchaseOrderCreate)) return Forbid();
            var model = new CreatePurchaseOrderRequest { Lines = new() { new() } };
            if (restockRequestId.HasValue)
            {
                var request = await _context.RestockRequests.AsNoTracking()
                    .Include(x => x.Ingredient)
                    .SingleOrDefaultAsync(x => x.RestockRequestId == restockRequestId.Value);
                if (request?.IngredientId == null) return BadRequest("Chỉ tạo đơn đặt hàng cho yêu cầu nhập nguyên liệu.");
                var allocation = await _allocations.GetSummaryAsync(request.RestockRequestId);
                var remaining = allocation?.RemainingUnallocatedQuantity ?? request.RequestedQuantity;
                if (remaining <= 0)
                {
                    TempData["ErrorMessage"] = "Yêu cầu nhập không còn số lượng chưa phân bổ để tạo đơn đặt hàng.";
                    return RedirectToAction("Details", "AdminRestockRequests", new { id = request.RestockRequestId });
                }
                model.StoreId = request.StoreId;
                model.Lines[0].RestockRequestId = request.RestockRequestId;
                model.Lines[0].IngredientId = request.IngredientId.Value;
                var offer = await _context.IngredientSuppliers.AsNoTracking()
                    .Where(x => x.IngredientId == request.IngredientId && x.Active)
                    .OrderByDescending(x => x.IsPrimary).ThenBy(x => x.IngredientSupplierId)
                    .FirstOrDefaultAsync();
                if (offer != null)
                {
                    model.SupplierId = offer.SupplierId;
                    model.Lines[0].IngredientSupplierId = offer.IngredientSupplierId;
                    model.Lines[0].ProcurementUnitId = request.ProcurementUnitId;
                    if ((!offer.PackageQuantity.HasValue || offer.PackageQuantity <= 0m)
                        && offer.AllowsLoosePurchase
                        && offer.LooseProcurementUnitId == request.ProcurementUnitId)
                    {
                        model.Lines[0].PurchaseMode = PurchaseMode.Loose;
                        model.Lines[0].OrderedProcurementQuantity = request.RequestedProcurementQuantity;
                        model.Lines[0].PackageCount = null;
                    }
                    else
                    {
                        var packageBaseQuantity = await _conversion.ConvertAsync(
                            request.IngredientId.Value,
                            offer.PackageQuantity.GetValueOrDefault(),
                            offer.UnitId,
                            request.Ingredient!.BaseUnitId);
                        model.Lines[0].PackageCount = Math.Max(
                            offer.MinimumOrderPackageCount.GetValueOrDefault(1),
                            packageBaseQuantity.IsSuccess && packageBaseQuantity.Data > 0
                                ? Math.Ceiling(remaining / packageBaseQuantity.Data)
                                : 1);
                    }
                }
            }
            var actor = _actor.Get(User);
            var storeScope = await _storeScopeResolver.ResolveAsync(
                actor,
                model.StoreId > 0 ? model.StoreId : null);
            if (!storeScope.IsResolved) return StoreScopeFailure(storeScope);
            model.StoreId = storeScope.StoreId!.Value;
            await PopulateAsync(model.StoreId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.PurchaseOrderCreate)]
        [RequirePermission(PermissionConstants.PurchaseAdviceSelectSupplier)]
        public async Task<IActionResult> CreateFromAdvice(CreatePurchaseOrderBatchRequest request)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.PurchaseOrderCreate))
                return Forbid();
            if (request.Lines.Select(x => x.PurchaseAdviceLineId).Distinct().Count() != 1)
            {
                TempData["Error"] = "Đơn đặt hàng thường chỉ nhận một nguồn đề nghị mua.";
                return RedirectToAction("Index", "AdminPurchaseAdviceConsolidation");
            }

            var selected = request.Lines.Single();
            var adviceLine = await _context.PurchaseAdviceLines
                .AsNoTracking()
                .Include(x => x.PurchaseAdvice)
                .Include(x => x.RestockRequest)
                .SingleOrDefaultAsync(x => x.PurchaseAdviceLineId == selected.PurchaseAdviceLineId);
            if (adviceLine == null)
            {
                TempData["Error"] = "Không tìm thấy dòng đề nghị mua đã chọn.";
                return RedirectToAction("Index", "AdminPurchaseAdviceConsolidation");
            }

            var actor = _actor.Get(User);
            CafeChain.Application.DTOs.AI.SupplierRecommendationDto? intelligenceSnapshot = null;
            if (_supplierIntelligence != null)
            {
                try
                {
                    var remainingBase = Math.Max(0m,
                        adviceLine.RequestedPurchaseBaseQuantity
                        - adviceLine.AllocatedToPoBaseQuantity
                        - adviceLine.ClosedBaseQuantity);
                    intelligenceSnapshot = await _supplierIntelligence.CompareAsync(
                        actor,
                        adviceLine.PurchaseAdvice.StoreId,
                        adviceLine.IngredientId,
                        remainingBase,
                        HttpContext.RequestAborted);
                }
                catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
                {
                    // Feature OFF/shadow rollout must never block manual procurement.
                }
            }
            var result = await _service.CreateDraftAsync(new CreatePurchaseOrderRequest
            {
                StoreId = adviceLine.PurchaseAdvice.StoreId,
                SupplierId = request.SupplierId,
                ExpectedDeliveryAtUtc = request.ExpectedDeliveryTo?.Date
                    ?? request.ExpectedDeliveryFrom?.Date
                    ?? adviceLine.NeededByDate,
                Note = request.Note,
                Lines =
                {
                    new CreatePurchaseOrderLineRequest
                    {
                        PurchaseAdviceLineId = adviceLine.PurchaseAdviceLineId,
                        PurchaseAdviceLineRowVersion = selected.RowVersion,
                        RestockRequestId = adviceLine.RestockRequestId,
                        RestockReferenceCode = adviceLine.RestockRequest.ReferenceCode,
                        IngredientId = adviceLine.IngredientId,
                        IngredientSupplierId = selected.IngredientSupplierId,
                        PurchaseMode = selected.PurchaseMode,
                        PackageCount = selected.PackageCount,
                        OrderedProcurementQuantity = selected.OrderedProcurementQuantity,
                        ProcurementUnitId = adviceLine.ProcurementUnitId
                    }
                }
            }, actor.StaffId, actor.RoleNames);

            if (!result.IsSuccess || result.Data == null)
            {
                TempData["Error"] = result.Message ?? "Không thể tạo đơn đặt hàng.";
                return RedirectToAction("Index", "AdminPurchaseAdviceConsolidation");
            }

            if (intelligenceSnapshot != null)
            {
                var selectedCandidate = intelligenceSnapshot.Candidates
                    .FirstOrDefault(x => x.SupplierId == request.SupplierId
                        && x.IngredientSupplierId == selected.IngredientSupplierId);
                _context.AuditLogs.Add(new AuditLog
                {
                    TableName = "SupplierIntelligenceSnapshot",
                    RecordId = result.Data.PurchaseOrderId,
                    Action = "SUPPLIER_SELECTED_FOR_PO",
                    UserId = actor.StaffId,
                    CreatedAt = DateTime.UtcNow,
                    NewData = JsonSerializer.Serialize(new
                    {
                        PurchaseOrderId = result.Data.PurchaseOrderId,
                        StoreId = adviceLine.PurchaseAdvice.StoreId,
                        adviceLine.IngredientId,
                        intelligenceSnapshot.RequiredBaseQuantity,
                        CandidateSuppliers = intelligenceSnapshot.Candidates,
                        SelectedSupplierId = request.SupplierId,
                        SelectedIngredientSupplierId = selected.IngredientSupplierId,
                        SelectedCandidate = selectedCandidate,
                        intelligenceSnapshot.WeightVersion,
                        intelligenceSnapshot.CalculatedAtUtc,
                        SelectingStaffId = actor.StaffId
                    })
                });
                await _context.SaveChangesAsync(HttpContext.RequestAborted);
            }

            TempData["SuccessMessage"] = "Đã tạo đơn đặt hàng thường từ đề nghị mua.";
            return RedirectToAction(nameof(Details), new { id = result.Data.PurchaseOrderId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.PurchaseOrderCreate)]
        public async Task<IActionResult> Create(CreatePurchaseOrderRequest model)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.PurchaseOrderCreate)) return Forbid();
            var actor = _actor.Get(User);
            var storeScope = await _storeScopeResolver.ResolveAsync(actor, model.StoreId);
            if (!storeScope.IsResolved) return StoreScopeFailure(storeScope);
            model.StoreId = storeScope.StoreId!.Value;
            var result = await _service.CreateDraftAsync(model, actor.StaffId, actor.RoleNames);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message;
                await PopulateAsync(model.StoreId);
                return View(model);
            }
            return RedirectToAction(nameof(Details), new { id = result.Data.PurchaseOrderId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.PurchaseOrderApprove)]
        public Task<IActionResult> Approve(int id, string rowVersion) => Transition(id, rowVersion, true);

        [HttpPost, ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.PurchaseOrderSend)]
        public Task<IActionResult> MarkSent(int id, string rowVersion) => Transition(id, rowVersion, false);

        [HttpPost, ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.PurchaseOrderCancel)]
        public async Task<IActionResult> Cancel(int id, string rowVersion, string reason)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.PurchaseOrderCancel)) return Forbid();
            var actor = _actor.Get(User);
            var result = await _service.CancelAsync(id, rowVersion, actor.StaffId, actor.RoleNames, reason);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.PurchaseOrderCloseRemaining)]
        public async Task<IActionResult> CloseLineRemaining(
            int id,
            int lineId,
            decimal closeBaseQuantity,
            string rowVersion,
            string reason,
            string requestKey)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.PurchaseOrderCloseRemaining)) return Forbid();
            var actor = _actor.Get(User);
            var result = await _service.CloseLineRemainingAsync(new ClosePurchaseOrderLineRemainingRequest
            {
                PurchaseOrderLineId = lineId,
                CloseBaseQuantity = closeBaseQuantity,
                RowVersion = rowVersion,
                Reason = reason,
                RequestKey = requestKey
            }, actor.StaffId, actor.RoleNames);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<IActionResult> Transition(int id, string rowVersion, bool approve)
        {
            var requiredPermission = approve
                ? PermissionConstants.PurchaseOrderApprove
                : PermissionConstants.PurchaseOrderSend;
            if (!await HasEffectivePermissionAsync(requiredPermission)) return Forbid();
            var actor = _actor.Get(User);
            var result = approve
                ? await _service.ApproveAsync(id, rowVersion, actor.StaffId, actor.RoleNames)
                : await _service.MarkSentAsync(id, rowVersion, actor.StaffId, actor.RoleNames);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task PopulateAsync(int storeId)
        {
            var actor = _actor.Get(User);
            var storeScope = await _storeScopeResolver.ResolveAsync(
                actor,
                storeId > 0 ? storeId : null);
            ViewBag.Stores = storeScope.AccessibleStores
                .Select(x => new CafeChain.Models.Stores.Store
                {
                    StoreId = x.StoreId,
                    Name = x.StoreName,
                    Active = true
                })
                .ToList();
            ViewBag.Suppliers = await _context.Suppliers.AsNoTracking().Where(x => x.Active).OrderBy(x => x.Name).ToListAsync();
            ViewBag.Offers = await _context.IngredientSuppliers.AsNoTracking()
                .Include(x => x.Ingredient).Include(x => x.Supplier).Include(x => x.Unit).Include(x => x.LooseProcurementUnit)
                .Where(x => x.Active && (storeId <= 0 || x.Supplier.SupplierStores.Any(s => s.StoreId == storeId && s.Active)))
                .OrderBy(x => x.Ingredient.Name).ToListAsync();
        }

    }
}
