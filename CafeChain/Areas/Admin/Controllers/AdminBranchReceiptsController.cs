using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Interfaces.Inventories;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// Issue #128 — Branch receipt draft / confirm for StoreManager & ShiftSupervisor (own store).
    /// </summary>
    public class AdminBranchReceiptsController : AdminBaseController
    {
        private readonly IBranchReceiptService _receiptService;

        public AdminBranchReceiptsController(IBranchReceiptService receiptService)
        {
            _receiptService = receiptService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? status = null)
        {
            if (!CanViewReceipts())
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xem phiếu nhận hàng.";
                return RedirectToAction("Index", "AdminRestockRequests");
            }

            var storeId = ResolveStoreId();
            if (storeId <= 0 && !IsGlobalRole())
                return Unauthorized();

            if (storeId <= 0)
            {
                TempData["ErrorMessage"] = "Chọn cửa hàng để xem phiếu nhận.";
                return View(new List<BranchReceiptListItemDto>());
            }

            var result = await _receiptService.ListForStoreAsync(
                storeId, ResolveStaffId(), storeId, RoleNames(), status);
            ViewBag.StatusFilter = status;
            ViewBag.StoreId = storeId;
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

            var storeId = ResolveStoreId();
            var result = await _receiptService.GetDetailAsync(id, ResolveStaffId(), storeId > 0 ? storeId : null, RoleNames());
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message ?? "Không tìm thấy phiếu nhận.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CanConfirm = CanConfirmReceipts() && result.Data.Status == BranchReceiptStatuses.Draft;
            return View(result.Data);
        }

        [HttpGet]
        public IActionResult Create(int? restockRequestId = null)
        {
            if (!CanConfirmReceipts())
            {
                TempData["ErrorMessage"] = "Bạn không có quyền tạo phiếu nhận.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.RestockRequestId = restockRequestId;
            ViewBag.StoreId = ResolveStoreId();
            return View(new CreateBranchReceiptRequest
            {
                StoreId = ResolveStoreId(),
                ReceiptKey = $"RCPT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}",
                Lines = restockRequestId.HasValue
                    ? new List<CreateBranchReceiptLineInput>
                    {
                        new() { RestockRequestId = restockRequestId.Value }
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

            var storeId = ResolveStoreId();
            if (storeId > 0)
                model.StoreId = storeId;

            var result = await _receiptService.CreateDraftAsync(model, ResolveStaffId(), RoleNames());
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message ?? "Không tạo được phiếu nhận.";
                ViewBag.RestockRequestId = model.Lines?.FirstOrDefault()?.RestockRequestId;
                ViewBag.StoreId = model.StoreId;
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

            var storeId = ResolveStoreId();
            var result = await _receiptService.ConfirmAsync(
                id, ResolveStaffId(), storeId > 0 ? storeId : null, RoleNames());

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

        private bool CanViewReceipts() =>
            User.IsInRole(RoleConstants.StoreManager)
            || User.IsInRole(RoleConstants.ShiftSupervisor)
            || User.IsInRole(RoleConstants.AccountantWarehouse)
            || User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.SystemAdmin)
            || User.IsInRole(RoleConstants.AreaManager);

        private bool CanConfirmReceipts() =>
            User.IsInRole(RoleConstants.StoreManager)
            || User.IsInRole(RoleConstants.ShiftSupervisor)
            || User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.SystemAdmin)
            || User.IsInRole(RoleConstants.AreaManager);

        private bool IsGlobalRole() =>
            User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.SystemAdmin)
            || User.IsInRole(RoleConstants.AreaManager)
            || User.IsInRole(RoleConstants.AccountantWarehouse);

        private int ResolveStoreId()
        {
            var claim = User.FindFirst("StoreId")?.Value;
            return int.TryParse(claim, out var id) && id > 0 ? id : 0;
        }

        private int ResolveStaffId()
        {
            var claim = User.FindFirst("StaffId")?.Value
                        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) && id > 0 ? id : 0;
        }

        private List<string> RoleNames() =>
            User.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                .Select(c => c.Value)
                .Distinct()
                .ToList();
    }
}
