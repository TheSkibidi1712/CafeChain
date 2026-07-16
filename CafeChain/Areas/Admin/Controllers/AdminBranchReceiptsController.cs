using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Inventories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CafeChain.Data;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// Issue #128 — Branch receipt draft / confirm with server-side business scope enforcement.
    /// Controller does not mutate inventory; BranchReceiptService owns posting.
    /// </summary>
    public class AdminBranchReceiptsController : AdminBaseController
    {
        private readonly IBranchReceiptService _receiptService;
        private readonly IAdminActorContextAccessor _actor;
        private readonly AppDbContext _context;

        public AdminBranchReceiptsController(
            IBranchReceiptService receiptService,
            IAdminActorContextAccessor actor,
            AppDbContext context)
        {
            _receiptService = receiptService;
            _actor = actor;
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
            var targetStoreId = HasCrossStoreDocumentRole()
                ? storeId.GetValueOrDefault()
                : ctx.StoreId;
            if (targetStoreId <= 0 && !HasCrossStoreDocumentRole())
                return Unauthorized();

            if (targetStoreId <= 0)
            {
                TempData["ErrorMessage"] = "Chọn cửa hàng để xem phiếu nhận.";
                ViewBag.CanCreate = false;
                return View(new List<BranchReceiptListItemDto>());
            }

            var result = await _receiptService.ListForStoreAsync(
                targetStoreId, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames, status);
            if (!result.IsSuccess)
                return Forbid();
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
            var targetStoreId = HasCrossStoreDocumentRole()
                ? storeId.GetValueOrDefault()
                : ctx.StoreId;
            if (targetStoreId <= 0)
            {
                TempData["ErrorMessage"] = "Cần chọn cửa hàng trước khi tạo phiếu nhận.";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.RestockRequestId = restockRequestId;
            ViewBag.StoreId = targetStoreId;
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
        public async Task<IActionResult> Create(CreateBranchReceiptRequest model)
        {
            if (!CanConfirmReceipts())
            {
                TempData["ErrorMessage"] = "Bạn không có quyền tạo phiếu nhận.";
                return RedirectToAction(nameof(Index));
            }

            var ctx = _actor.Get(User);
            if (!HasCrossStoreDocumentRole() && ctx.StoreId > 0)
                model.StoreId = ctx.StoreId;

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
        public async Task<IActionResult> Confirm(int id)
        {
            if (!CanConfirmReceipts())
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xác nhận phiếu nhận.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ctx = _actor.Get(User);
            var result = await _receiptService.ConfirmAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames);

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
            var targetStoreId = HasCrossStoreDocumentRole() ? storeId : ctx.StoreId;
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
            var targetStoreId = HasCrossStoreDocumentRole() ? storeId : ctx.StoreId;
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
            || User.IsInRole(RoleConstants.SalesStaff)
            || User.IsInRole(RoleConstants.AccountantWarehouse)
            || User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.AreaManager);

        private bool CanConfirmReceipts() =>
            User.IsInRole(RoleConstants.StoreManager)
            || User.IsInRole(RoleConstants.AccountantWarehouse)
            || User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.AreaManager);

        private bool HasCrossStoreDocumentRole() =>
            User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.AreaManager)
            || User.IsInRole(RoleConstants.AccountantWarehouse);
    }
}
