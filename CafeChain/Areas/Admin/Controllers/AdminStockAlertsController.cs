using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Inventories;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// Issue #99 — StoreManager confirm/reject StockAlert (Admin MVC).
    /// Issue #100 — create RestockRequest from CONFIRMED alert.
    /// </summary>
    public class AdminStockAlertsController : AdminBaseController
    {
        private readonly IStockAlertManagerService _service;
        private readonly IRestockRequestService _restockService;

        public AdminStockAlertsController(
            IStockAlertManagerService service,
            IRestockRequestService restockService)
        {
            _service = service;
            _restockService = restockService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? status = "OPEN", int page = 1)
        {
            if (!IsStoreManager())
            {
                TempData["ErrorMessage"] = "Chỉ Quản lý chi nhánh được xem/xử lý cảnh báo kho.";
                return RedirectToAction("Index", "AdminNotifications");
            }

            var storeId = ResolveStoreId();
            var staffId = ResolveStaffId();
            if (storeId <= 0 || staffId <= 0)
                return Unauthorized();

            var result = await _service.ListForStoreAsync(storeId, status, page, 20);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message ?? "Không tải được danh sách cảnh báo.";
                return View(result.Data ?? new Application.DTOs.Admin.StockAlerts.StockAlertListResultDto());
            }

            ViewBag.StatusFilter = status;
            ViewBag.IsStoreManager = true;
            return View(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!IsStoreManager())
            {
                TempData["ErrorMessage"] = "Chỉ Quản lý chi nhánh được xem/xử lý cảnh báo kho.";
                return RedirectToAction(nameof(Index));
            }

            var storeId = ResolveStoreId();
            if (storeId <= 0)
                return Unauthorized();

            var result = await _service.GetDetailAsync(id, storeId);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message ?? "Không tìm thấy cảnh báo.";
                return RedirectToAction(nameof(Index));
            }

            var openRestock = await _restockService.GetOpenByAlertAsync(id, storeId);
            ViewBag.OpenRestockRequest = openRestock.IsSuccess ? openRestock.Data : null;
            ViewBag.IsStoreManager = true;
            return View(result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int id, string managerNote)
        {
            if (!IsStoreManager())
            {
                TempData["ErrorMessage"] = "Chỉ Quản lý chi nhánh được xác nhận cảnh báo.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var staffId = ResolveStaffId();
            var storeId = ResolveStoreId();
            if (staffId <= 0 || storeId <= 0)
                return Unauthorized();

            var result = await _service.ConfirmAsync(id, staffId, storeId, managerNote ?? string.Empty);
            if (!result.IsSuccess)
                TempData["ErrorMessage"] = result.Message;
            else
                TempData["SuccessMessage"] = result.Message ?? "Đã xác nhận cảnh báo kho.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string rejectReason)
        {
            if (!IsStoreManager())
            {
                TempData["ErrorMessage"] = "Chỉ Quản lý chi nhánh được báo sai cảnh báo.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var staffId = ResolveStaffId();
            var storeId = ResolveStoreId();
            if (staffId <= 0 || storeId <= 0)
                return Unauthorized();

            var result = await _service.RejectAsync(id, staffId, storeId, rejectReason ?? string.Empty);
            if (!result.IsSuccess)
                TempData["ErrorMessage"] = result.Message;
            else
                TempData["SuccessMessage"] = result.Message ?? "Đã báo sai cảnh báo kho.";

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRestockRequest(
            int id,
            decimal requestedQuantity,
            string? priority,
            string? note)
        {
            if (!IsStoreManager())
            {
                TempData["ErrorMessage"] = "Chỉ Quản lý chi nhánh được tạo yêu cầu nhập hàng.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var staffId = ResolveStaffId();
            var storeId = ResolveStoreId();
            if (staffId <= 0 || storeId <= 0)
                return Unauthorized();

            var result = await _restockService.CreateFromConfirmedAlertAsync(
                id, staffId, storeId, requestedQuantity, note, priority);

            if (!result.IsSuccess)
                TempData["ErrorMessage"] = result.Message;
            else
                TempData["SuccessMessage"] = result.Message ?? "Đã gửi yêu cầu nhập hàng cho Kế toán/kho.";

            return RedirectToAction(nameof(Details), new { id });
        }

        private bool IsStoreManager() =>
            User.IsInRole(RoleConstants.StoreManager);

        private int ResolveStaffId()
        {
            var claim = User.FindFirst("StaffId")?.Value;
            return int.TryParse(claim, out var id) && id > 0 ? id : 0;
        }

        private int ResolveStoreId()
        {
            var claim = User.FindFirst("StoreId")?.Value;
            return int.TryParse(claim, out var id) && id > 0 ? id : 0;
        }
    }
}
