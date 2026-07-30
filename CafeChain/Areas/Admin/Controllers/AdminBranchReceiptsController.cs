using CafeChain.Application.Constants;
using CafeChain.Application.Authorization;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Interfaces.Inventories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CafeChain.Data;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// Issue #128 — Branch receipt draft / confirm with server-side business scope enforcement.
    /// Controller does not mutate inventory; BranchReceiptService owns posting.
    /// </summary>
    [RequirePermission(PermissionConstants.ReceiptView,
        RoleConstants.BusinessOwner + "," +
        RoleConstants.AreaManager + "," +
        RoleConstants.StoreManager + "," +
        RoleConstants.ShiftSupervisor + "," +
        RoleConstants.AccountantWarehouse)]
    public class AdminBranchReceiptsController : AdminStoreScopedController
    {
        private readonly IBranchReceiptService _receiptService;
        private readonly IAdminActorContextAccessor _actor;
        private readonly IAdminStoreScopeResolver _storeScopeResolver;
        private readonly AppDbContext _context;

        public AdminBranchReceiptsController(
            IBranchReceiptService receiptService,
            IAdminActorContextAccessor actor,
            IAdminStoreScopeResolver storeScopeResolver,
            AppDbContext context)
        {
            _receiptService = receiptService;
            _actor = actor;
            _storeScopeResolver = storeScopeResolver;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? status = null, int? storeId = null)
        {
            if (!CanViewReceipts())
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xem phiếu nhận hàng.";
                return RedirectToAction("Index", "AdminRestockRequests");
            }

            var ctx = _actor.Get(User);
            if (ctx.StaffId <= 0)
                return Unauthorized();
            var storeScope = await _storeScopeResolver.ResolveAsync(ctx, storeId);
            if (!storeScope.IsResolved)
                return StoreScopeFailure(storeScope);
            var targetStoreId = storeScope.StoreId!.Value;

            var result = await _receiptService.ListForStoreAsync(
                targetStoreId, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames, status);
            if (!result.IsSuccess)
                return Forbid();
            SetStoreScopeViewData(storeScope);
            ViewBag.StatusFilter = status;
            ViewBag.StoreId = targetStoreId;
            ViewBag.CanCreate = CanConfirmReceipts();
            return View(result.Data ?? new List<BranchReceiptListItemDto>());
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!CanViewReceipts())
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xem phiếu nhận hàng.";
                return RedirectToAction(nameof(Index));
            }

            var ctx = _actor.Get(User);
            var result = await _receiptService.GetDetailAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message ?? "Không tìm thấy phiếu nhận.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CanConfirm = CanConfirmReceipts() && result.Data.Status == BranchReceiptStatuses.Draft;
            return View(result.Data);
        }

        [HttpGet]
        [RequirePermission(PermissionConstants.ReceiptCreate)]
        public async Task<IActionResult> ReceivePurchaseOrder(int purchaseOrderId)
        {
            if (!CanConfirmReceipts())
            {
                TempData["ErrorMessage"] = "Bạn không có quyền nhận hàng tại cửa hàng.";
                return RedirectToAction(nameof(Index));
            }

            var ctx = _actor.Get(User);
            var result = await _receiptService.CreateOrOpenPurchaseOrderDraftAsync(
                purchaseOrderId, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message ?? "Không tạo được phiếu kiểm đếm từ PO.";
                return RedirectToAction("Details", "AdminPurchaseOrders", new { id = purchaseOrderId });
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(EditPurchaseOrderDraft), new { id = result.Data.BranchReceiptId });
        }

        [HttpGet]
        [RequirePermission(PermissionConstants.ReceiptUpdateDraft)]
        public async Task<IActionResult> EditPurchaseOrderDraft(int id)
        {
            if (!CanConfirmReceipts())
                return Forbid();

            var ctx = _actor.Get(User);
            var result = await _receiptService.GetPurchaseOrderDraftAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message ?? "Không tìm thấy phiếu kiểm đếm PO.";
                return RedirectToAction(nameof(Index));
            }

            return View("PurchaseOrderDraft", result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.ReceiptUpdateDraft)]
        public async Task<IActionResult> SavePurchaseOrderDraft(SavePurchaseOrderReceiptDraftRequest model)
        {
            if (!CanConfirmReceipts())
                return Forbid();

            var ctx = _actor.Get(User);
            var result = await _receiptService.SavePurchaseOrderDraftAsync(
                model, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message ?? "Không lưu được phiếu kiểm đếm.";
                var reload = await _receiptService.GetPurchaseOrderDraftAsync(
                    model.BranchReceiptId, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames);
                if (!reload.IsSuccess || reload.Data == null)
                    return RedirectToAction(nameof(Index));

                reload.Data.ReferenceNumber = model.ReferenceNumber;
                reload.Data.Notes = model.Notes;
                foreach (var line in reload.Data.Lines)
                {
                    var posted = model.Lines.FirstOrDefault(x => x.PurchaseOrderLineId == line.PurchaseOrderLineId);
                    if (posted == null) continue;
                    line.ActualReceivedQuantity = posted.ActualReceivedQuantity;
                    line.RejectedQuantity = posted.RejectedQuantity;
                    line.RejectionReason = posted.RejectionReason;
                    line.RejectionIssueType = posted.RejectionIssueType;
                }
                return View("PurchaseOrderDraft", reload.Data);
            }

            TempData["SuccessMessage"] = result.Message ?? "Đã lưu phiếu kiểm đếm nháp.";
            return RedirectToAction(nameof(Details), new { id = result.Data.BranchReceiptId });
        }

        [HttpGet]
        [RequirePermission(PermissionConstants.ReceiptCreate)]
        public async Task<IActionResult> Create(int? restockRequestId = null, int? storeId = null, int? purchaseOrderLineId = null)
        {
            if (!CanConfirmReceipts())
            {
                TempData["ErrorMessage"] = "Bạn không có quyền tạo phiếu nhận.";
                return RedirectToAction(nameof(Index));
            }

            var ctx = _actor.Get(User);
            CafeChain.Models.Inventories.Procurement.PurchaseOrderLine? poLine = null;
            if (purchaseOrderLineId.HasValue)
            {
                poLine = await _context.PurchaseOrderLines.AsNoTracking()
                    .Include(x => x.PurchaseOrder)
                    .SingleOrDefaultAsync(x => x.PurchaseOrderLineId == purchaseOrderLineId.Value);
                if (poLine == null) return NotFound();
                restockRequestId = poLine.RestockRequestId;
                storeId = poLine.PurchaseOrder.StoreId;
            }
            var storeScope = await _storeScopeResolver.ResolveAsync(ctx, storeId);
            if (!storeScope.IsResolved)
                return StoreScopeFailure(storeScope);
            var targetStoreId = storeScope.StoreId!.Value;
            ViewBag.RestockRequestId = restockRequestId;
            ViewBag.StoreId = targetStoreId;
            SetStoreScopeViewData(storeScope);
            await PopulateSupplierOptionsAsync(targetStoreId, ctx);
            return View(new CreateBranchReceiptRequest
            {
                StoreId = targetStoreId,
                SupplierId = poLine?.PurchaseOrder.SupplierId,
                ReceiptKey = $"RCPT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}",
                Lines = restockRequestId.HasValue
                    ? new List<CreateBranchReceiptLineInput>
                    {
                        new()
                        {
                            RestockRequestId = restockRequestId.Value,
                            PurchaseOrderLineId = poLine?.PurchaseOrderLineId,
                            IngredientSupplierId = poLine?.IngredientSupplierId
                        }
                    }
                    : new List<CreateBranchReceiptLineInput> { new() }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.ReceiptCreate)]
        public async Task<IActionResult> Create(CreateBranchReceiptRequest model)
        {
            if (!CanConfirmReceipts())
            {
                TempData["ErrorMessage"] = "Bạn không có quyền tạo phiếu nhận.";
                return RedirectToAction(nameof(Index));
            }

            var ctx = _actor.Get(User);
            var storeScope = await _storeScopeResolver.ResolveAsync(ctx, model.StoreId);
            if (!storeScope.IsResolved)
                return StoreScopeFailure(storeScope);
            model.StoreId = storeScope.StoreId!.Value;

            var result = await _receiptService.CreateDraftAsync(model, ctx.StaffId, ctx.RoleNames);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message ?? "Không tạo được phiếu nhận.";
                ViewBag.RestockRequestId = model.Lines?.FirstOrDefault()?.RestockRequestId;
                ViewBag.StoreId = model.StoreId;
                await PopulateSupplierOptionsAsync(model.StoreId, ctx);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message ?? "Đã tạo phiếu nhận nháp.";
            return RedirectToAction(nameof(Details), new { id = result.Data.BranchReceiptId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.ReceiptConfirm)]
        public async Task<IActionResult> Confirm(int id, string? rowVersion)
        {
            if (!CanConfirmReceipts())
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xác nhận phiếu nhận.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ctx = _actor.Get(User);
            var result = await _receiptService.ConfirmAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames, rowVersion);

            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Message ?? "Xác nhận thất bại.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (result.Data?.AlertEvaluationFailed == true)
                TempData["WarningMessage"] = result.Message ?? "Đã nhập kho nhưng cập nhật cảnh báo thất bại.";
            else
                TempData["SuccessMessage"] = result.Message ?? "Đã xác nhận và nhập kho.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> SupplierOptions(int storeId)
        {
            if (!CanConfirmReceipts())
                return Forbid();

            var ctx = _actor.Get(User);
            var storeScope = await _storeScopeResolver.ResolveAsync(ctx, storeId);
            if (!storeScope.IsResolved)
                return StoreScopeApiFailure(storeScope);
            var targetStoreId = storeScope.StoreId!.Value;
            var result = await _receiptService.GetSupplierOptionsAsync(
                targetStoreId, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames);
            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message, errorCode = result.ErrorCode });
            return Json(new { success = true, data = result.Data });
        }

        [HttpGet]
        public async Task<IActionResult> OfferOptions(int storeId, int supplierId, int? restockRequestId)
        {
            if (!CanConfirmReceipts())
                return Forbid();

            var ctx = _actor.Get(User);
            var storeScope = await _storeScopeResolver.ResolveAsync(ctx, storeId);
            if (!storeScope.IsResolved)
                return StoreScopeApiFailure(storeScope);
            var targetStoreId = storeScope.StoreId!.Value;
            var result = await _receiptService.GetOfferOptionsAsync(
                targetStoreId,
                supplierId,
                restockRequestId,
                ctx.StaffId,
                ctx.StoreIdOrNull,
                ctx.RoleNames);
            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message, errorCode = result.ErrorCode });
            return Json(new { success = true, data = result.Data });
        }

        private async Task PopulateSupplierOptionsAsync(
            int storeId,
            CafeChain.Application.DTOs.Admin.Actor.AdminActorContext actor)
        {
            var result = await _receiptService.GetSupplierOptionsAsync(
                storeId, actor.StaffId, actor.StoreIdOrNull, actor.RoleNames);
            ViewBag.SupplierOptions = result.IsSuccess
                ? result.Data ?? new List<BranchReceiptSupplierOptionDto>()
                : new List<BranchReceiptSupplierOptionDto>();
        }

        private bool CanViewReceipts() =>
            User.IsInRole(RoleConstants.StoreManager)
            || User.IsInRole(RoleConstants.ShiftSupervisor)
            || User.IsInRole(RoleConstants.AccountantWarehouse)
            || User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.AreaManager)
            || User.IsInRole(RoleConstants.SystemAdmin);

        private bool CanConfirmReceipts() =>
            User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.StoreManager)
            || User.IsInRole(RoleConstants.ShiftSupervisor)
            || User.IsInRole(RoleConstants.SystemAdmin);

        private IActionResult StoreScopeApiFailure(
            CafeChain.Application.DTOs.Admin.StoreScope.AdminStoreScopeResolution resolution)
        {
            var statusCode = resolution.Status
                == CafeChain.Application.DTOs.Admin.StoreScope.AdminStoreScopeResolutionStatus.StoreNotFound
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status403Forbidden;
            return StatusCode(statusCode, new
            {
                success = false,
                errorCode = resolution.ErrorCode,
                message = resolution.Message
            });
        }
    }
}
