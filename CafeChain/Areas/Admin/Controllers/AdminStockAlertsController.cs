using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.StoreScope;
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
        private readonly IAdminActorContextAccessor _actor;
        private readonly IAdminStoreScopeResolver _storeScopeResolver;

        public AdminStockAlertsController(
            IStockAlertManagerService service,
            IRestockRequestService restockService,
            IAdminActorContextAccessor actor,
            IAdminStoreScopeResolver storeScopeResolver)
        {
            _service = service;
            _restockService = restockService;
            _actor = actor;
            _storeScopeResolver = storeScopeResolver;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? status = "OPEN", int page = 1, int? storeId = null)
        {
            if (!CanView())
            {
                TempData["ErrorMessage"] = "Chỉ Quản lý chi nhánh được xem/xử lý cảnh báo kho.";
                return RedirectToAction("Index", "AdminNotifications");
            }

            var actor = _actor.Get(User);
            if (actor.StaffId <= 0)
                return Unauthorized();
            var storeScope = await _storeScopeResolver.ResolveAsync(actor, storeId);
            if (!storeScope.IsResolved)
                return StoreScopeFailure(storeScope);
            var targetStoreId = storeScope.StoreId!.Value;

            var result = await _service.ListForStoreAsync(targetStoreId, status, page, 20);
            SetStoreScopeViewData(storeScope);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message ?? "Không tải được danh sách cảnh báo.";
                return View(result.Data ?? new Application.DTOs.Admin.StockAlerts.StockAlertListResultDto());
            }

            ViewBag.StatusFilter = status;
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

            var actor = _actor.Get(User);
            if (actor.StaffId <= 0)
                return Unauthorized();
            var storeScope = await _storeScopeResolver.ResolveAsync(actor, storeId);
            if (!storeScope.IsResolved)
                return StoreScopeFailure(storeScope);
            var targetStoreId = storeScope.StoreId!.Value;

            var result = await _service.GetDetailAsync(id, targetStoreId);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message ?? "Không tìm thấy cảnh báo.";
                return RedirectToAction(nameof(Index));
            }

            var openRestock = await _restockService.GetOpenByAlertAsync(id, targetStoreId);
            ViewBag.OpenRestockRequest = openRestock.IsSuccess ? openRestock.Data : null;
            SetStoreScopeViewData(storeScope);
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

            var actor = _actor.Get(User);
            if (actor.StaffId <= 0)
                return Unauthorized();
            var storeScope = await _storeScopeResolver.ResolveAsync(actor, storeId);
            if (!storeScope.IsResolved)
                return StoreScopeFailure(storeScope);
            var targetStoreId = storeScope.StoreId!.Value;

            var result = await _service.ConfirmAsync(id, actor.StaffId, targetStoreId, managerNote ?? string.Empty, rowVersion);
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

            var actor = _actor.Get(User);
            if (actor.StaffId <= 0)
                return Unauthorized();
            var storeScope = await _storeScopeResolver.ResolveAsync(actor, storeId);
            if (!storeScope.IsResolved)
                return StoreScopeFailure(storeScope);
            var targetStoreId = storeScope.StoreId!.Value;

            var result = await _service.RejectAsync(id, actor.StaffId, targetStoreId, rejectReason ?? string.Empty, rowVersion);
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
            var actor = _actor.Get(User);
            if (actor.StaffId <= 0)
                return Unauthorized();
            var storeScope = await _storeScopeResolver.ResolveAsync(actor, storeId);
            if (!storeScope.IsResolved)
                return StoreScopeFailure(storeScope);
            var targetStoreId = storeScope.StoreId!.Value;
            var result = await _service.CloseAsync(
                id,
                actor.StaffId,
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

            var actor = _actor.Get(User);
            if (actor.StaffId <= 0)
                return Unauthorized();
            var storeScope = await _storeScopeResolver.ResolveAsync(actor, storeId);
            if (!storeScope.IsResolved)
                return StoreScopeFailure(storeScope);
            var targetStoreId = storeScope.StoreId!.Value;

            var result = await _restockService.CreateFromConfirmedAlertAsync(
                id, actor.StaffId, targetStoreId, requestedQuantity, note, priority);

            if (!result.IsSuccess)
                TempData["ErrorMessage"] = result.Message;
            else
                TempData["SuccessMessage"] = result.Message ?? "Đã tạo yêu cầu nhập hàng nháp.";

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

    }
}
