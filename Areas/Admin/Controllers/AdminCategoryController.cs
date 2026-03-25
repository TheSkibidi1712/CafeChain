using CafeChain.Application.DTOs.Admin.Categories;
using CafeChain.Application.Interfaces.Admin.Categories;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminCategoryController : Controller
    {
        private readonly IAdminCategoryService _categoryService;

        public AdminCategoryController(IAdminCategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            var pagedCategories = await _categoryService.GetPaginatedCategoriesAsync(page, 6);
            return View(pagedCategories);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminCreateCategoryDto dto)
        {
            if (ModelState.IsValid)
            {
                if (await _categoryService.CheckCategoryNameExistAsync(dto.Name))
                {
                    TempData["CreateError"] = "Tên danh mục đã tồn tại.";
                    return RedirectToAction(nameof(Index));
                }

                await _categoryService.CreateCategoryAsync(dto);
                return RedirectToAction(nameof(Index));
            }
            
            TempData["CreateError"] = "Thông tin không hợp lệ, vui lòng kiểm tra lại.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Category/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _categoryService.GetCategoryByIdAsync(id);
            if (category == null)
            {
                return NotFound();
            }

            var dto = new AdminUpdateCategoryDto
            {
                CategoryId = category.CategoryId,
                Name = category.Name,
                Active = category.Active
            };

            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AdminUpdateCategoryDto dto)
        {
            if (id != dto.CategoryId)
            {
                return BadRequest();
            }

            if (ModelState.IsValid)
            {
                if (await _categoryService.CheckCategoryNameExistAsync(dto.Name, dto.CategoryId))
                {
                    ModelState.AddModelError("Name", "Tên danh mục đã tồn tại.");
                    return View(dto);
                }

                await _categoryService.UpdateCategoryAsync(dto);
                return RedirectToAction(nameof(Index));
            }
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            await _categoryService.ToggleCategoryStatusAsync(id);
            return RedirectToAction(nameof(Index));
        }

    }
}
