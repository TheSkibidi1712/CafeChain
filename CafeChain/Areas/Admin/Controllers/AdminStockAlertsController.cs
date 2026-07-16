using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
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
        private readonly IScopeAuthorizationService _scopeAuthorization;

        public AdminStockAlertsController(
            IStockAlertManagerService service,
            IRestockRequestService restockService,
            IScopeAuthorizationService scopeAuthorization)
        {
            _service = service;
            _restockService = restockService;
            _scopeAuthorization = scopeAuthorization;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? status = "OPEN", int page = 1, int? storeId = null)
        {
            if (!CanView())
            {
                TempData["ErrorMessage"] = "Chỉ Quản lý chi nhánh được xem/xử lý cảnh báo kho.";
                return RedirectToAction("Index", "AdminNotifications");
            }

            var staffId = ResolveStaffId();
            var targetStoreId = await ResolveAuthorizedStoreIdAsync(staffId, storeId, mutation: false);
            if (targetStoreId <= 0 || staffId <= 0)
                return Unauthorized();

            var result = await _service.ListForStoreAsync(targetStoreId, status, page, 20);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message ?? "Không tải được danh sách cảnh báo.";
                return View(result.Data ?? new Application.DTOs.Admin.StockAlerts.StockAlertListResultDto());
            }

            ViewBag.StatusFilter = status;
            ViewBag.SelectedStoreId = targetStoreId;
            ViewBag.IsStoreManager = CanManage();
            return View(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, int? storeId = null)
        {
            if (!CanView())
            {
                TempData["ErrorMessage"] = "Chỉ Quản lý chi nhánh được xem/xử lý cảnh báo kho.";
                return RedirectToAction(nameof(Index));
            }

            var targetStoreId = await ResolveAuthorizedStoreIdAsync(ResolveStaffId(), storeId, mutation: false);
            if (targetStoreId <= 0)
                return Unauthorized();

            var result = await _service.GetDetailAsync(id, targetStoreId);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message ?? "Không tìm thấy cảnh báo.";
                return RedirectToAction(nameof(Index));
            }

            var openRestock = await _restockService.GetOpenByAlertAsync(id, targetStoreId);
            ViewBag.OpenRestockRequest = openRestock.IsSuccess ? openRestock.Data : null;
            ViewBag.SelectedStoreId = targetStoreId;
            ViewBag.IsStoreManager = CanManage();
            return View(result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int id, string managerNote, string? rowVersion, int? storeId = null)
        {
            if (!CanManage())
            {
                TempData["ErrorMessage"] = "Chỉ Quản lý chi nhánh được xác nhận cảnh báo.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var staffId = ResolveStaffId();
            var targetStoreId = await ResolveAuthorizedStoreIdAsync(staffId, storeId, mutation: true);
            if (staffId <= 0 || targetStoreId <= 0)
                return Unauthorized();

            var result = await _service.ConfirmAsync(id, staffId, targetStoreId, managerNote ?? string.Empty, rowVersion);
            if (!result.IsSuccess)
                TempData["ErrorMessage"] = result.Message;
            else
                TempData["SuccessMessage"] = result.Message ?? "Đã xác nhận cảnh báo kho.";

            return RedirectToAction(nameof(Details), new { id, storeId = targetStoreId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string rejectReason, string? rowVersion, int? storeId = null)
        {
            if (!CanManage())
            {
                TempData["ErrorMessage"] = "Chỉ Quản lý chi nhánh được báo sai cảnh báo.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var staffId = ResolveStaffId();
            var targetStoreId = await ResolveAuthorizedStoreIdAsync(staffId, storeId, mutation: true);
            if (staffId <= 0 || targetStoreId <= 0)
                return Unauthorized();

            var result = await _service.RejectAsync(id, staffId, targetStoreId, rejectReason ?? string.Empty, rowVersion);
            if (!result.IsSuccess)
                TempData["ErrorMessage"] = result.Message;
            else
                TempData["SuccessMessage"] = result.Message ?? "Đã báo sai cảnh báo kho.";

            return RedirectToAction(nameof(Details), new { id, storeId = targetStoreId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(int id, string closeReason, string? rowVersion, int? storeId = null)
        {
            if (!CanManage())
                return Forbid();
            var staffId = ResolveStaffId();
            var targetStoreId = await ResolveAuthorizedStoreIdAsync(staffId, storeId, mutation: true);
            if (staffId <= 0 || targetStoreId <= 0)
                return Unauthorized();
            var result = await _service.CloseAsync(
                id,
                staffId,
                targetStoreId,
                closeReason ?? string.Empty,
                rowVersion);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new { id, storeId = targetStoreId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRestockRequest(
            int id,
            decimal requestedQuantity,
            string? priority,
            string? note,
            int? storeId = null)
        {
            if (!CanManage())
            {
                TempData["ErrorMessage"] = "Chỉ Quản lý chi nhánh được tạo yêu cầu nhập hàng.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var staffId = ResolveStaffId();
            var targetStoreId = await ResolveAuthorizedStoreIdAsync(staffId, storeId, mutation: true);
            if (staffId <= 0 || targetStoreId <= 0)
                return Unauthorized();

            var result = await _restockService.CreateFromConfirmedAlertAsync(
                id, staffId, targetStoreId, requestedQuantity, note, priority);

            if (!result.IsSuccess)
                TempData["ErrorMessage"] = result.Message;
            else
                TempData["SuccessMessage"] = result.Message ?? "Đã gửi yêu cầu nhập hàng cho Kế toán/kho.";

            return RedirectToAction(nameof(Details), new { id, storeId = targetStoreId });
        }

        private bool CanView() =>
            CanManage()
            || User.IsInRole(RoleConstants.AreaManager)
            || User.IsInRole(RoleConstants.AccountantWarehouse)
            || User.IsInRole(RoleConstants.ShiftSupervisor)
            || User.IsInRole(RoleConstants.SalesStaff);

        private bool CanManage() =>
            User.IsInRole(RoleConstants.StoreManager)
            || User.IsInRole(RoleConstants.BusinessOwner);

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

        private async Task<int> ResolveAuthorizedStoreIdAsync(
            int staffId,
            int? requestedStoreId,
            bool mutation)
        {
            var actorStoreId = ResolveStoreId();
            if (User.IsInRole(RoleConstants.StoreManager)
                || User.IsInRole(RoleConstants.ShiftSupervisor)
                || User.IsInRole(RoleConstants.SalesStaff))
                return actorStoreId > 0 && (!requestedStoreId.HasValue || requestedStoreId == actorStoreId)
                    ? actorStoreId
                    : 0;
            if (User.IsInRole(RoleConstants.BusinessOwner)
                || (!mutation && User.IsInRole(RoleConstants.AccountantWarehouse)))
                return requestedStoreId.GetValueOrDefault();
            if (User.IsInRole(RoleConstants.AreaManager)
                && requestedStoreId.GetValueOrDefault() > 0
                && await _scopeAuthorization.CanAccessStoreAsync(staffId, requestedStoreId.Value))
                return requestedStoreId.Value;
            return 0;
        }
    }
}
