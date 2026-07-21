using CafeChain.Application.DTOs.Admin.Ingredients;
using CafeChain.Application.Interfaces.Admin.Ingredients;
using CafeChain.Application.Authorization;
using CafeChain.Application.Constants;
using CafeChain.ViewModels.Admin.Ingredients;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Areas.Admin.Controllers
{
    [RequirePermission(PermissionConstants.IngredientView)]
    public class AdminIngredientController : AdminBaseController
    {
        private readonly IAdminIngredientService _service;

        public AdminIngredientController(IAdminIngredientService service)
        {
            _service = service;
        }

        // ================= INDEX =================
        // ================= INDEX =================
        public async Task<IActionResult> Index(string? search, bool? status, int page = 1)
        {
            int pageSize = 10;

            var (data, total) = await _service.GetPagedAsync(
                search,
                status,
                page,
                pageSize);

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.Search = search;
            ViewBag.Status = status;

            return View(data);
        }

        // ================= CREATE =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.IngredientCreate)]
        public async Task<IActionResult> Create([FromBody] AdminIngredientCreateDTO dto)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

            try
            {
                var id = await _service.CreateAsync(dto); // 🔥 return ID
                return Json(new { success = true, message = "Thêm thành công", data = id });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ================= GET BY ID =================
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var data = await _service.GetByIdAsync(id);

            if (data == null)
                return Json(new { success = false, message = "Không tìm thấy" });

            return Json(new { success = true, data });
        }

        // ================= UPDATE =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.IngredientUpdate)]
        public async Task<IActionResult> Update([FromBody] AdminIngredientUpdateDTO dto)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

            try
            {
                await _service.UpdateAsync(dto);
                return Json(new { success = true, message = "Cập nhật thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ================= TOGGLE =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.IngredientToggleStatus)]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                await _service.ToggleStatusAsync(id);
                return Json(new { success = true, message = "Đã cập nhật trạng thái" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ================= GET UNITS =================
        [HttpGet]
        public async Task<IActionResult> GetUnits()
        {
            var units = await _service.GetUnitsAsync();

            return Json(units.Select(x => new
            {
                id = x.UnitId,
                text = $"{x.Name} ({x.UnitCode})",
                type = x.Type // 🔥 gửi xuống JS
            }));
        }
    }
}

