using CafeChain.Application.Constants;
using CafeChain.Application.Authorization;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Admin.StoreInventories;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// Issue #99 — StoreManager confirm/reject StockAlert (Admin MVC).
    /// Issue #100 — create RestockRequest from CONFIRMED alert.
    /// </summary>
    [RequirePermission(PermissionConstants.StockAlertView)]
    public class AdminStockAlertsController : AdminBaseController
    {
        private readonly IStockAlertManagerService _service;
        private readonly IRestockRequestService _restockService;
        private readonly IAdminActorContextAccessor _actor;
        private readonly IAdminStoreScopeResolver _storeScopeResolver;
        private readonly IPreparedItemReplenishmentReadService? _preparedItemReplenishment;

        public AdminStockAlertsController(
            IStockAlertManagerService service,
            IRestockRequestService restockService,
            IAdminActorContextAccessor actor,
            IAdminStoreScopeResolver storeScopeResolver,
            IPreparedItemReplenishmentReadService? preparedItemReplenishment = null)
        {
            _service = service;
            _restockService = restockService;
            _actor = actor;
            _storeScopeResolver = storeScopeResolver;
            _preparedItemReplenishment = preparedItemReplenishment;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? status = "OPEN", int page = 1, int? storeId = null)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.StockAlertView))
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
            ViewBag.IsStoreManager = await HasEffectivePermissionAsync(PermissionConstants.StockAlertResolve);
            return View(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, int? storeId = null)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.StockAlertView))
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
            if (result.Data.PreparedItemId.HasValue && _preparedItemReplenishment != null)
            {
                var replenishment = await _preparedItemReplenishment.GetAsync(
                    actor.AccountId,
                    targetStoreId,
                    result.Data.PreparedItemId.Value);
                ViewBag.PreparedItemReplenishment = replenishment.IsSuccess
                    ? replenishment.Data
                    : null;
            }
            SetStoreScopeViewData(storeScope);
            ViewBag.IsStoreManager = await HasEffectivePermissionAsync(PermissionConstants.StockAlertResolve);
            return View(result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.StockAlertResolve)]
        public async Task<IActionResult> Confirm(int id, string managerNote, string? rowVersion, int? storeId = null)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.StockAlertResolve))
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
        [RequirePermission(PermissionConstants.StockAlertResolve)]
        public async Task<IActionResult> Reject(int id, string rejectReason, string? rowVersion, int? storeId = null)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.StockAlertResolve))
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
        [RequirePermission(PermissionConstants.StockAlertResolve)]
        public async Task<IActionResult> Close(int id, string closeReason, string? rowVersion, int? storeId = null)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.StockAlertResolve))
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
        [RequirePermission(PermissionConstants.StockAlertCreateRestockRequest)]
        public async Task<IActionResult> CreateRestockRequest(
            int id,
            decimal requestedProcurementQuantity,
            int procurementUnitId,
            string? priority,
            string? note,
            string? rowVersion,
            int? storeId = null)
        {
            if (!await HasEffectivePermissionAsync(PermissionConstants.StockAlertCreateRestockRequest))
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

            var result = await _restockService.CreatePreparedItemDemandFromConfirmedAlertAsync(
                id,
                actor.StaffId,
                actor.AccountId,
                targetStoreId,
                rowVersion,
                note,
                priority);

            if (!result.IsSuccess
                && result.ErrorCode == "ALERT_NOT_CANONICAL_PREPARED_ITEM")
            {
                result = await _restockService.CreateFromConfirmedAlertProcurementAsync(
                    id,
                    actor.StaffId,
                    targetStoreId,
                requestedProcurementQuantity,
                procurementUnitId,
                note,
                priority);
            }

            if (!result.IsSuccess)
                TempData["ErrorMessage"] = result.Message;
            else
                TempData["SuccessMessage"] = result.Message ?? "Đã tạo nhu cầu bổ sung.";

            return RedirectToAction(nameof(Details), new { id, storeId = targetStoreId });
        }

    }
}
