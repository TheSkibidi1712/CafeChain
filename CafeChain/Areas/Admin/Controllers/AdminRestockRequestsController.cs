using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.RestockRequests;
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

        public AdminRestockRequestsController(
            IRestockRequestService service,
            IRestockRequestWorkflowService workflow)
        {
            _service = service;
            _workflow = workflow;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? status = "SUBMITTED", int page = 1)
        {
            if (!CanViewRestockRequests())
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xem yêu cầu nhập hàng.";
                return RedirectToAction("Index", "AdminNotifications");
            }

            var storeId = ResolveStoreId();
            if (storeId <= 0)
                return Unauthorized();

            var result = await _service.ListForStoreAsync(storeId, status, page, 20);
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

            var storeId = ResolveStoreId();
            var roles = RoleNames();
            var staffId = ResolveStaffId();

            var result = await _workflow.GetWorkflowDetailAsync(
                id, staffId, storeId > 0 ? storeId : null, roles);

            if (!result.IsSuccess || result.Data == null)
            {
                // Fallback to simple detail for store-scoped list users
                if (storeId > 0)
                {
                    var simple = await _service.GetDetailAsync(id, storeId);
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

            var result = await _workflow.StartProcessingAsync(
                id, ResolveStaffId(), ResolveStoreIdOrNull(), RoleNames(), reason);
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

            var result = await _workflow.RejectAsync(
                id, ResolveStaffId(), ResolveStoreIdOrNull(), RoleNames(), reason);
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

            var result = await _workflow.CancelAsync(
                id, ResolveStaffId(), ResolveStoreIdOrNull(), RoleNames(), reason);
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

            var result = await _workflow.LinkFulfillmentAsync(
                id, ResolveStaffId(), ResolveStoreIdOrNull(), RoleNames(), model);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
                result.Message ?? (result.IsSuccess ? "OK" : "Thất bại");
            return RedirectToAction(nameof(Details), new { id });
        }

        private bool CanViewRestockRequests() =>
            User.IsInRole(RoleConstants.StoreManager)
            || User.IsInRole(RoleConstants.AccountantWarehouse)
            || User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.SystemAdmin)
            || User.IsInRole(RoleConstants.AreaManager)
            || User.IsInRole(RoleConstants.ShiftSupervisor);

        private bool CanWarehouseActions() =>
            User.IsInRole(RoleConstants.AccountantWarehouse)
            || User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.SystemAdmin)
            || User.IsInRole(RoleConstants.AreaManager);

        private bool CanCreateReceipt() =>
            User.IsInRole(RoleConstants.StoreManager)
            || User.IsInRole(RoleConstants.ShiftSupervisor)
            || User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.SystemAdmin)
            || User.IsInRole(RoleConstants.AreaManager);

        private bool CanCancel() =>
            CanWarehouseActions() || User.IsInRole(RoleConstants.StoreManager);

        private int ResolveStoreId()
        {
            var claim = User.FindFirst("StoreId")?.Value;
            return int.TryParse(claim, out var id) && id > 0 ? id : 0;
        }

        private int? ResolveStoreIdOrNull()
        {
            var id = ResolveStoreId();
            return id > 0 ? id : null;
        }

        private int ResolveStaffId()
        {
            var claim = User.FindFirst("StaffId")?.Value;
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
