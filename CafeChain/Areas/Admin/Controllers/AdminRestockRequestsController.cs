using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Interfaces.Inventories;
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
        private readonly IAdminStoreScopeResolver _storeScopeResolver;

        public AdminRestockRequestsController(
            IRestockRequestService service,
            IRestockRequestWorkflowService workflow,
            IAdminActorContextAccessor actor,
            IAdminStoreScopeResolver storeScopeResolver)
        {
            _service = service;
            _workflow = workflow;
            _actor = actor;
            _storeScopeResolver = storeScopeResolver;
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
            if (ctx.StaffId <= 0)
                return Unauthorized();
            var storeScope = await _storeScopeResolver.ResolveAsync(ctx, storeId);
            if (!storeScope.IsResolved)
                return StoreScopeFailure(storeScope);
            var targetStoreId = storeScope.StoreId!.Value;

            var result = await _service.ListForStoreAsync(targetStoreId, status, page, 20);
            SetStoreScopeViewData(storeScope);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message ?? "Không tải được danh sách yêu cầu.";
                return View(result.Data ?? new RestockRequestListResultDto());
            }

            ViewBag.StatusFilter = status;
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
                        ViewBag.CanSubmit = CanSubmit();
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
                            RowVersion = simple.Data.RowVersion,
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
            ViewBag.CanSubmit = CanSubmit();
            return View(result.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int id, string? rowVersion)
        {
            if (!CanSubmit())
            {
                TempData["ErrorMessage"] = "Không có quyền gửi yêu cầu nhập hàng.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ctx = _actor.Get(User);
            var result = await _workflow.SubmitAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames, rowVersion);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.Message ?? (result.IsSuccess ? "OK" : "Thất bại");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartProcessing(int id, string? reason, string? rowVersion)
        {
            if (!CanWarehouseActions())
            {
                TempData["ErrorMessage"] = "Không có quyền chuyển PROCESSING.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ctx = _actor.Get(User);
            var result = await _workflow.StartProcessingAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames, reason, rowVersion);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.Message ?? (result.IsSuccess ? "OK" : "Thất bại");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string reason, string? rowVersion)
        {
            if (!CanWarehouseActions())
            {
                TempData["ErrorMessage"] = "Không có quyền từ chối.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ctx = _actor.Get(User);
            var result = await _workflow.RejectAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames, reason, rowVersion);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.Message ?? (result.IsSuccess ? "OK" : "Thất bại");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string? reason, string? rowVersion)
        {
            if (!CanCancel())
            {
                TempData["ErrorMessage"] = "Không có quyền hủy.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ctx = _actor.Get(User);
            var result = await _workflow.CancelAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames, reason, rowVersion);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.Message ?? (result.IsSuccess ? "OK" : "Thất bại");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CloseRemaining(int id, string reason, string? rowVersion)
        {
            if (!CanWarehouseActions())
            {
                TempData["ErrorMessage"] = "Không có quyền đóng phần còn lại.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ctx = _actor.Get(User);
            var result = await _workflow.CloseRemainingAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames, reason, rowVersion);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.Message ?? (result.IsSuccess ? "OK" : "Thất bại");
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LinkFulfillment(int id, LinkRestockFulfillmentRequest model, string? rowVersion)
        {
            if (!CanWarehouseActions())
            {
                TempData["ErrorMessage"] = "Không có quyền gắn fulfillment.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var ctx = _actor.Get(User);
            var result = await _workflow.LinkFulfillmentAsync(
                id, ctx.StaffId, ctx.StoreIdOrNull, ctx.RoleNames, model, rowVersion);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.Message ?? (result.IsSuccess ? "OK" : "Thất bại");
            return RedirectToAction(nameof(Details), new { id });
        }

        private bool CanViewRestockRequests() =>
            User.IsInRole(RoleConstants.StoreManager)
            || User.IsInRole(RoleConstants.AccountantWarehouse)
            || User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.AreaManager);

        private bool CanWarehouseActions() =>
            User.IsInRole(RoleConstants.AccountantWarehouse)
            || User.IsInRole(RoleConstants.BusinessOwner);

        private bool CanCreateReceipt() =>
            User.IsInRole(RoleConstants.StoreManager)
            || User.IsInRole(RoleConstants.AccountantWarehouse)
            || User.IsInRole(RoleConstants.BusinessOwner);

        private bool CanCancel() =>
            CanWarehouseActions() || User.IsInRole(RoleConstants.StoreManager);

        private bool CanSubmit() =>
            User.IsInRole(RoleConstants.StoreManager)
            || User.IsInRole(RoleConstants.BusinessOwner);

    }
}
