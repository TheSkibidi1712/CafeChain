using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Inventories;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// Issue #100 — read-only RestockRequest list/detail for StoreManager and AccountantWarehouse.
    /// </summary>
    public class AdminRestockRequestsController : AdminBaseController
    {
        private readonly IRestockRequestService _service;

        public AdminRestockRequestsController(IRestockRequestService service)
        {
            _service = service;
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
                return View(result.Data ?? new Application.DTOs.Admin.RestockRequests.RestockRequestListResultDto());
            }

            ViewBag.StatusFilter = status;
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
            if (storeId <= 0)
                return Unauthorized();

            var result = await _service.GetDetailAsync(id, storeId);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message ?? "Không tìm thấy yêu cầu.";
                return RedirectToAction(nameof(Index));
            }

            return View(result.Data);
        }

        private bool CanViewRestockRequests() =>
            User.IsInRole(RoleConstants.StoreManager) ||
            User.IsInRole(RoleConstants.AccountantWarehouse);

        private int ResolveStoreId()
        {
            var claim = User.FindFirst("StoreId")?.Value;
            return int.TryParse(claim, out var id) && id > 0 ? id : 0;
        }
    }
}
