using CafeChain.Application.Interfaces.Operations;
using CafeChain.Application.Services.Operations;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// Issue #101 — Admin read-only notifications for StoreManager / AccountantWarehouse
    /// (and other Admin panel roles that have StaffId claim).
    /// Uses cookie auth StaffId — does not grant POS sales access.
    /// </summary>
    public class AdminNotificationsController : AdminBaseController
    {
        private readonly IStaffNotificationQueryService _service;

        public AdminNotificationsController(IStaffNotificationQueryService service)
        {
            _service = service;
        }

        // GET: /Admin/AdminNotifications
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
        {
            var staffId = ResolveStaffId();
            if (staffId <= 0)
                return Unauthorized();

            var list = await _service.GetListAsync(
                staffId,
                page,
                pageSize,
                StaffNotificationQueryService.ChannelAdmin);

            if (!list.IsSuccess || list.Data == null)
            {
                TempData["ErrorMessage"] = list.Message ?? "Không tải được thông báo.";
                return View(new CafeChain.Application.DTOs.POS.StaffNotificationListDto());
            }

            ViewBag.Page = list.Data.Page;
            ViewBag.PageSize = list.Data.PageSize;
            ViewBag.Total = list.Data.Total;
            ViewBag.UnreadCount = list.Data.UnreadCount;
            return View(list.Data);
        }

        [HttpGet]
        public async Task<IActionResult> UnreadCount()
        {
            var staffId = ResolveStaffId();
            if (staffId <= 0)
                return Unauthorized();

            var result = await _service.GetUnreadCountAsync(staffId);
            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message });

            return Json(new { success = true, data = result.Data });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            var staffId = ResolveStaffId();
            if (staffId <= 0)
                return Unauthorized();

            var result = await _service.MarkReadAsync(staffId, id);
            if (!result.IsSuccess)
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var staffId = ResolveStaffId();
            if (staffId <= 0)
                return Unauthorized();

            var result = await _service.MarkAllReadAsync(staffId);
            if (!result.IsSuccess)
                TempData["ErrorMessage"] = result.Message;
            else
                TempData["SuccessMessage"] = $"Đã đánh dấu {result.Data?.MarkedCount ?? 0} thông báo là đã đọc.";

            return RedirectToAction(nameof(Index));
        }

        /// <summary>JSON endpoints for optional AJAX badge (same auth as Admin).</summary>
        [HttpGet]
        public async Task<IActionResult> ListJson(int page = 1, int pageSize = 20)
        {
            var staffId = ResolveStaffId();
            if (staffId <= 0)
                return Unauthorized();

            var result = await _service.GetListAsync(
                staffId, page, pageSize, StaffNotificationQueryService.ChannelAdmin);
            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message });

            return Json(new { success = true, data = result.Data });
        }

        [HttpPost]
        public async Task<IActionResult> MarkReadJson(int id)
        {
            var staffId = ResolveStaffId();
            if (staffId <= 0)
                return Unauthorized();

            var result = await _service.MarkReadAsync(staffId, id);
            if (!result.IsSuccess)
                return NotFound(new { success = false, message = result.Message });

            return Json(new { success = true, data = result.Data });
        }

        private int ResolveStaffId()
        {
            var claim = User.FindFirst("StaffId")?.Value;
            return int.TryParse(claim, out var id) && id > 0 ? id : 0;
        }
    }
}
