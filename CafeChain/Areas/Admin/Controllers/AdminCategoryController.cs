using CafeChain.Application.DTOs.Admin.Categories;
using CafeChain.Application.Interfaces.Admin.Categories;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    public class AdminCategoryController
        : AdminBaseController
    {
        private readonly IAdminCategoryService
            _categoryService;

        public AdminCategoryController(
            IAdminCategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            var categories = await _categoryService.GetPaginatedCategoriesAsync(page, 6);

            return View(categories);
        }

        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var category = await _categoryService.GetCategoryForEditAsync(id);

            if (category == null)
            {
                return Error( "Không tìm thấy danh mục.", StatusCodes.Status404NotFound);
            }

            return Json(new
            {
                success = true,
                data = category
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminCreateCategoryDto dto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationError();
            }

            if (await _categoryService.CheckCategoryNameExistAsync(dto.Name))
            {
                return Error("Tên danh mục đã tồn tại.", StatusCodes.Status409Conflict);
            }

            await _categoryService.CreateCategoryAsync(dto);

            return Success("Thêm danh mục thành công.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminUpdateCategoryDto dto)
        {
            if (!ModelState.IsValid)
            {
                return ValidationError();
            }

            if (await _categoryService.CheckCategoryNameExistAsync(dto.Name, dto.CategoryId))
            {
                return Error("Tên danh mục đã tồn tại.", StatusCodes.Status409Conflict);
            }

            var result = await _categoryService.UpdateCategoryAsync(dto);

            if (result == null)
            {
                return Error("Không tìm thấy danh mục.", StatusCodes.Status404NotFound);
            }

            return Success("Cập nhật danh mục thành công.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var success = await _categoryService.ToggleCategoryStatusAsync(id);

            if (!success)
            {
                return Error("Không tìm thấy danh mục.", StatusCodes.Status404NotFound);
            }

            return Success("Cập nhật trạng thái thành công.");
        }

        #region Private Helpers

        private JsonResult Success(string message)
        {
            return Json(new
            {   
                success = true,
                message
            });
        }

        private JsonResult Error(string message, int statusCode = StatusCodes.Status400BadRequest)
        {
            Response.StatusCode = statusCode;

            return Json(new
            {
                success = false,
                message
            });
        }

        private JsonResult ValidationError()
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;

            return Json(new
            {
                success = false,
                message = "Thông tin không hợp lệ.",
                errors = ModelState
                    .Where(x => x.Value.Errors.Any())
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value.Errors
                            .Select(e => e.ErrorMessage)
                            .ToArray())
            });
        }

        #endregion
    }
}