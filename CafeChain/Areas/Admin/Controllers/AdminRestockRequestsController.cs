using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// Issue #100 list/detail + Issue #128 workflow actions (intent-only; no inventory mutation).
    /// </summary>
    public class AdminRestockRequestsController : AdminBaseController
    {
        private readonly IRestockRequestService _service;
        private readonly IRestockRequestWorkflowService _workflow;
        private readonly IAdminActorContextAccessor _actor;
        private readonly IScopeAuthorizationService _scopeAuthorization;

        public AdminRestockRequestsController(
            IRestockRequestService service,
            IRestockRequestWorkflowService workflow,
            IAdminActorContextAccessor actor,
            IScopeAuthorizationService scopeAuthorization)
        {
            _service = service;
            _workflow = workflow;
            _actor = actor;
            _scopeAuthorization = scopeAuthorization;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? status = "SUBMITTED",
            int page = 1,
            int? storeId = null)
        {
            if (!CanViewRestockRequests())
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xem yêu cầu nhập hàng.";
                return RedirectToAction("Index", "AdminNotifications");
            }

            var ctx = _actor.Get(User);
            var targetStoreId = await ResolveAuthorizedStoreIdAsync(ctx.StaffId, ctx.StoreId, storeId);
            if (targetStoreId <= 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn cửa hàng nằm trong phạm vi được phân quyền.";
                return Unauthorized();
            }

            var result = await _service.ListForStoreAsync(targetStoreId, status, page, 20);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message ?? "Không tải được danh sách yêu cầu.";
                return View(result.Data ?? new RestockRequestListResultDto());
            }

            ViewBag.StatusFilter = status;
            ViewBag.SelectedStoreId = targetStoreId;
            ViewBag.CanWarehouse = CanWarehouseActions();
            ViewBag.CanCreateReceipt = CanCreateReceipt();
            return View(result.Data);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!CanViewRestockRequests())
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xem yêu cầu nhập hàng.";
                return RedirectToAction(nameof(Index));
            }

            var ctx = _actor.Get(User);
            var result = await _workflow.GetWorkflowDetailAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames);

            if (!result.IsSuccess || result.Data == null)
            {
                if (ctx.StoreId > 0)
                {
                    var simple = await _service.GetDetailAsync(id, ctx.StoreId);
                    if (simple.IsSuccess && simple.Data != null)
                    {
                        ViewBag.CanWarehouse = CanWarehouseActions();
                        ViewBag.CanCreateReceipt = CanCreateReceipt();
                        ViewBag.CanCancel = CanCancel();
                        return View("Details", new RestockRequestWorkflowDetailDto
                        {
                            RestockRequestId = simple.Data.RestockRequestId,
                            StockAlertId = simple.Data.StockAlertId,
                            StoreId = simple.Data.StoreId,
                            StoreName = simple.Data.StoreName,
                            ItemName = simple.Data.ItemName,
                            ItemTypeLabel = simple.Data.ItemTypeLabel,
                            RequestedQuantity = simple.Data.RequestedQuantity,
                            SuggestedQuantity = simple.Data.SuggestedQuantity,
                            Status = simple.Data.Status,
                            Priority = simple.Data.Priority,
                            Note = simple.Data.Note,
                            CreatedByName = simple.Data.CreatedByName,
                            CreatedAt = simple.Data.CreatedAt,
                            UpdatedAt = simple.Data.UpdatedAt,
                            IngredientId = simple.Data.IngredientId,
                            RecipeId = simple.Data.RecipeId,
                            PreparedItemId = simple.Data.PreparedItemId,
                            CreatedByStaffId = simple.Data.CreatedByStaffId,
                            AlertType = simple.Data.AlertType,
                            AlertStatus = simple.Data.AlertStatus,
                            AlertCurrentQtySnapshot = simple.Data.AlertCurrentQtySnapshot,
                            AlertThresholdSnapshot = simple.Data.AlertThresholdSnapshot,
                            RemainingQuantity = simple.Data.RequestedQuantity
                        });
                    }
                }

                TempData["ErrorMessage"] = result.Message ?? "Không tìm thấy yêu cầu.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CanWarehouse = CanWarehouseActions();
            ViewBag.CanCreateReceipt = CanCreateReceipt();
            ViewBag.CanCancel = CanCancel();
            return View(result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartProcessing(int id, string? reason = null)
        {
            if (!CanWarehouseActions())
            {
                TempData["ErrorMessage"] = "Không có quyền chuyển PROCESSING.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ctx = _actor.Get(User);
            var result = await _workflow.StartProcessingAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames, reason);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.Message ?? (result.IsSuccess ? "OK" : "Thất bại");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            if (!CanWarehouseActions())
            {
                TempData["ErrorMessage"] = "Không có quyền từ chối.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ctx = _actor.Get(User);
            var result = await _workflow.RejectAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames, reason);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.Message ?? (result.IsSuccess ? "OK" : "Thất bại");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string? reason = null)
        {
            if (!CanCancel())
            {
                TempData["ErrorMessage"] = "Không có quyền hủy.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ctx = _actor.Get(User);
            var result = await _workflow.CancelAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames, reason);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.Message ?? (result.IsSuccess ? "OK" : "Thất bại");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LinkFulfillment(int id, LinkRestockFulfillmentRequest model)
        {
            if (!CanWarehouseActions())
            {
                TempData["ErrorMessage"] = "Không có quyền gắn fulfillment.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ctx = _actor.Get(User);
            var result = await _workflow.LinkFulfillmentAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames, model);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.Message ?? (result.IsSuccess ? "OK" : "Thất bại");
            return RedirectToAction(nameof(Details), new { id });
        }

        private bool CanViewRestockRequests() =>
            User.IsInRole(RoleConstants.StoreManager)
            || User.IsInRole(RoleConstants.AccountantWarehouse)
            || User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.AreaManager)
            || User.IsInRole(RoleConstants.ShiftSupervisor)
            || User.IsInRole(RoleConstants.SalesStaff);

        private bool CanWarehouseActions() =>
            User.IsInRole(RoleConstants.AccountantWarehouse)
            || User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.AreaManager);

        private bool CanCreateReceipt() =>
            User.IsInRole(RoleConstants.StoreManager)
            || User.IsInRole(RoleConstants.AccountantWarehouse)
            || User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.AreaManager);

        private bool CanCancel() =>
            CanWarehouseActions() || User.IsInRole(RoleConstants.StoreManager);

        private async Task<int> ResolveAuthorizedStoreIdAsync(
            int staffId,
            int actorStoreId,
            int? requestedStoreId)
        {
            if (User.IsInRole(RoleConstants.StoreManager)
                || User.IsInRole(RoleConstants.ShiftSupervisor)
                || User.IsInRole(RoleConstants.SalesStaff))
                return actorStoreId > 0 && (!requestedStoreId.HasValue || requestedStoreId == actorStoreId)
                    ? actorStoreId
                    : 0;
            if (User.IsInRole(RoleConstants.BusinessOwner)
                || User.IsInRole(RoleConstants.AccountantWarehouse))
                return requestedStoreId.GetValueOrDefault();
            if (User.IsInRole(RoleConstants.AreaManager)
                && requestedStoreId.GetValueOrDefault() > 0
                && await _scopeAuthorization.CanAccessStoreAsync(staffId, requestedStoreId.Value))
                return requestedStoreId.Value;
            return 0;
        }
    }
}
