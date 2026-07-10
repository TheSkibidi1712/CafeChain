using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Categories;
using CafeChain.Application.Interfaces.Admin.Categories;
using CafeChain.Application.Interfaces.Admin.Permissions;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminCategoryController : AdminBaseController
    {
        private readonly IAdminCategoryService _categoryService;
        private readonly IAdminPermissionService _permissionService;

        public AdminCategoryController(
            IAdminCategoryService categoryService,
            IAdminPermissionService permissionService)
        {
            _categoryService = categoryService;
            _permissionService = permissionService;
        }

        // =====================================================
        // INDEX
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Index(CategoryFilterDto filter)
        {
            var guard = await EnsureCategoryPermissionAsync(
                PermissionConstants.CategoryView,
                jsonResponse: false);

            if (guard != null)
            {
                return guard;
            }

            filter.Page = filter.Page <= 0 ? 1 : filter.Page;

            filter.PageSize = filter.PageSize <= 0 ? 10 : filter.PageSize;

            var vm = await _categoryService.GetIndexDataAsync(filter);

            return View(vm);
        }

        // =====================================================
        // GET CATEGORY
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var guard = await EnsureCategoryPermissionAsync(
                PermissionConstants.CategoryUpdate);

            if (guard != null)
            {
                return guard;
            }

            var category = await _categoryService.GetCategoryForEditAsync(id);

            if (category == null)
            {
                return Error(
                    "Không tìm thấy danh mục.",
                    StatusCodes.Status404NotFound);
            }

            return Json(new
            {
                success = true,
                data = category
            });
        }

        // =====================================================
        // CREATE
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminCreateCategoryDto dto)
        {
            var guard = await EnsureCategoryPermissionAsync(
                PermissionConstants.CategoryCreate);

            if (guard != null)
            {
                return guard;
            }

            if (!ModelState.IsValid)
            {
                return ValidationError();
            }

            if (await _categoryService.CheckCategoryNameExistAsync(dto.Name))
            {
                return Error(
                    "Tên danh mục đã tồn tại.",
                    StatusCodes.Status409Conflict);
            }

            await _categoryService.CreateCategoryAsync(dto);

            return Success("Thêm danh mục thành công.");
        }

        // =====================================================
        // UPDATE
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminUpdateCategoryDto dto)
        {
            var guard = await EnsureCategoryPermissionAsync(
                PermissionConstants.CategoryUpdate);

            if (guard != null)
            {
                return guard;
            }

            if (!ModelState.IsValid)
            {
                return ValidationError();
            }

            if (await _categoryService.CheckCategoryNameExistAsync(
                dto.Name,
                dto.CategoryId))
            {
                return Error(
                    "Tên danh mục đã tồn tại.",
                    StatusCodes.Status409Conflict);
            }

            var result = await _categoryService.UpdateCategoryAsync(dto);

            if (result == null)
            {
                return Error(
                    "Không tìm thấy danh mục.",
                    StatusCodes.Status404NotFound);
            }

            return Success("Cập nhật danh mục thành công.");
        }

        // =====================================================
        // TOGGLE STATUS
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var guard = await EnsureCategoryPermissionAsync(
                PermissionConstants.CategoryToggleStatus);

            if (guard != null)
            {
                return guard;
            }

            var success = await _categoryService.ToggleCategoryStatusAsync(id);

            if (!success)
            {
                return Error(
                    "Không tìm thấy danh mục.",
                    StatusCodes.Status404NotFound);
            }

            return Success("Cập nhật trạng thái thành công.");
        }

        // =====================================================
        // PRIVATE METHODS
        // =====================================================

        private async Task<IActionResult?> EnsureCategoryPermissionAsync(
            string permissionCode,
            bool jsonResponse = true)
        {
            var accountIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(accountIdValue, out var accountId))
            {
                return jsonResponse
                    ? Error("Unauthorized.", StatusCodes.Status401Unauthorized)
                    : RedirectToAction("AccessDenied", "Account", new { area = "" });
            }

            var decision = await _permissionService.HasPermissionAsync(
                accountId,
                permissionCode);

            if (!decision.IsSuccess ||
                decision.Data == null ||
                !decision.Data.Allowed)
            {
                return jsonResponse
                    ? PermissionDenied()
                    : RedirectToAction("AccessDenied", "Account", new { area = "" });
            }

            return null;
        }

        private JsonResult PermissionDenied()
        {
            return Error(
                "Bạn không có quyền thực hiện chức năng này.",
                StatusCodes.Status403Forbidden);
        }

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
    }
}
