using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Categories;
using CafeChain.Application.Interfaces.Admin.Categories;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.DTOs.AI;
using Microsoft.AspNetCore.Mvc;
using CafeChain.Application.Exceptions;
using CafeChain.ViewModels.Admin.Categories;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminCategoryController : AdminBaseController
    {
        private readonly IAdminCategoryService _categoryService;
        private readonly IAdminPermissionService _permissionService;
        private readonly IAIService _aiService;

        public AdminCategoryController(
            IAdminCategoryService categoryService,
            IAdminPermissionService permissionService,
            IAIService aiService)
        {
            _categoryService = categoryService;
            _permissionService = permissionService;
            _aiService = aiService;
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
        public async Task<IActionResult> AiSuggestions([FromBody] CategorySuggestionRequestDTO? request)
        {
            var guard = await EnsureCategoryPermissionAsync(PermissionConstants.CategoryCreate);
            if (guard != null) return guard;

            if (!ModelState.IsValid)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Json(new
                {
                    success = false,
                    message = "Dữ liệu yêu cầu AI không hợp lệ.",
                    errorCode = "AI_INVALID_REQUEST"
                });
            }

            request ??= new CategorySuggestionRequestDTO();
            var result = await _aiService.SuggestCategoriesAsync(request, HttpContext.RequestAborted);
            Response.StatusCode = result.Success
                ? StatusCodes.Status200OK
                : result.ErrorCode switch
                {
                    "AI_TIMEOUT" => StatusCodes.Status504GatewayTimeout,
                    "AI_UNAVAILABLE" => StatusCodes.Status503ServiceUnavailable,
                    "AI_INVALID_RESPONSE" => StatusCodes.Status422UnprocessableEntity,
                    _ => StatusCodes.Status400BadRequest
                };
            return Json(new
            {
                success = result.Success,
                message = result.Message,
                errorCode = result.ErrorCode,
                data = result,
                usedOllama = result.UsedOllama,
                usedFallback = result.UsedFallback
            });
        }

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

            var duplicate = await _categoryService.CheckCategoryUniquenessAsync(
                dto.Name, dto.CategoryCode, cancellationToken: HttpContext.RequestAborted);
            if (duplicate.NameExists || duplicate.CodeExists)
            {
                return DuplicateConflict(duplicate.NameExists, duplicate.CodeExists);
            }

            try
            {
                await _categoryService.CreateCategoryAsync(dto);
            }
            catch (DuplicateDataException ex)
            {
                return Error(ex.Message, StatusCodes.Status409Conflict);
            }
            catch (ArgumentException ex)
            {
                return Error(ex.Message);
            }

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

            var duplicate = await _categoryService.CheckCategoryUniquenessAsync(
                dto.Name, dto.CategoryCode, dto.CategoryId, HttpContext.RequestAborted);
            if (duplicate.NameExists || duplicate.CodeExists)
            {
                return DuplicateConflict(duplicate.NameExists, duplicate.CodeExists);
            }

            AdminCategoryViewModel? result;
            try
            {
                result = await _categoryService.UpdateCategoryAsync(dto);
            }
            catch (DuplicateDataException ex)
            {
                return Error(ex.Message, StatusCodes.Status409Conflict);
            }
            catch (ArgumentException ex)
            {
                return Error(ex.Message);
            }

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

        private JsonResult DuplicateConflict(bool nameExists, bool codeExists)
        {
            var message = nameExists && codeExists
                ? "Tên và mã danh mục đã tồn tại."
                : nameExists ? "Tên danh mục đã tồn tại." : "Mã danh mục đã tồn tại.";
            Response.StatusCode = StatusCodes.Status409Conflict;
            var errors = new Dictionary<string, string[]>();
            if (nameExists) errors["Name"] = ["Tên danh mục đã tồn tại."];
            if (codeExists) errors["CategoryCode"] = ["Mã danh mục đã tồn tại."];
            return Json(new
            {
                success = false,
                message,
                errors
            });
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
