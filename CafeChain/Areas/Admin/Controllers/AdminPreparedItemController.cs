using CafeChain.Application.DTOs.Admin.PreparedItems;
using CafeChain.Application.Interfaces.Admin.PreparedItems;
using CafeChain.Helpers;
using CafeChain.ViewModels.Admin.PreparedItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// PreparedItem master CRUD — ADR-0006 / Issue #116.
    /// View: RequireAdminPanelAccess (base).
    /// Write: SystemAdmin, BusinessOwner, AccountantWarehouse only.
    /// </summary>
    public class AdminPreparedItemController : AdminBaseController
    {
        private readonly IAdminPreparedItemService _service;

        public AdminPreparedItemController(IAdminPreparedItemService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, bool? status, int page = 1)
        {
            const int pageSize = 10;
            var (data, total) = await _service.GetPagedAsync(search, status, page, pageSize);
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)total / pageSize));
            // Single source of truth for UI CTAs — must match Create/Update/SetActive Authorize roles.
            var canWrite = RoleHelper.CanWritePreparedItems(User);

            var vm = new AdminPreparedItemIndexPageVM
            {
                Items = data,
                Search = search,
                Status = status,
                Page = page,
                TotalPages = totalPages,
                TotalCount = total,
                CanWrite = canWrite
            };

            // Belt-and-suspenders for any residual ViewBag reads after #129 typed VM migration.
            ViewBag.CanWrite = canWrite;
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var data = await _service.GetByIdAsync(id);
            if (data == null)
                return Json(new { success = false, message = "Không tìm thấy bán thành phẩm." });
            return Json(new { success = true, data });
        }

        [HttpGet]
        public async Task<IActionResult> GetUnits()
        {
            var data = await _service.GetInventoryUnitsAsync();
            return Json(new { success = true, data });
        }

        [HttpGet]
        public async Task<IActionResult> GetBomOptions(string? search = null)
        {
            var data = await _service.GetBomOptionsAsync(search);
            return Json(new { success = true, data });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleHelper.PreparedItemWriteRoles)]
        public async Task<IActionResult> Create([FromBody] AdminPreparedItemSaveDTO dto)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            try
            {
                var id = await _service.CreateAsync(dto);
                return Json(new { success = true, message = "Thêm bán thành phẩm thành công.", data = id });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleHelper.PreparedItemWriteRoles)]
        public async Task<IActionResult> Update([FromBody] AdminPreparedItemSaveDTO dto)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ." });

            try
            {
                await _service.UpdateAsync(dto);
                return Json(new { success = true, message = "Cập nhật bán thành phẩm thành công." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleHelper.PreparedItemWriteRoles)]
        public async Task<IActionResult> SetActive([FromBody] AdminPreparedItemToggleDTO dto)
        {
            try
            {
                await _service.SetActiveAsync(dto.PreparedItemId, dto.Active);
                return Json(new
                {
                    success = true,
                    message = dto.Active ? "Đã kích hoạt bán thành phẩm." : "Đã ngưng bán thành phẩm."
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
