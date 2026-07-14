using CafeChain.Application.DTOs.Admin.Drinks;
using CafeChain.Application.Interfaces.Admin.Drinks;
using CafeChain.ViewModels.Admin.Drinks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.AI;

namespace CafeChain.Areas.Admin.Controllers
{
    public class AdminDrinkController : AdminBaseController
    {
        private readonly IAdminDrinkService _drinkService;
        private readonly IAdminPermissionService _permissionService;
        private readonly IAIService _aiService;
        private readonly IAIImagePipelineService _aiImagePipeline;

        public AdminDrinkController(
            IAdminDrinkService drinkService,
            IAdminPermissionService permissionService,
            IAIService aiService,
            IAIImagePipelineService aiImagePipeline)
        {
            _drinkService = drinkService;
            _permissionService = permissionService;
            _aiService = aiService;
            _aiImagePipeline = aiImagePipeline;
        }

        [HttpGet]
        public async Task<IActionResult> IndexPartial(AdminDrinkFilterDTO filter)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.DrinkView);
            if (guard != null) return guard;
            var viewModel = await _drinkService.GetIndexDataAsync(filter);

            return PartialView("_DrinkTablePartial", viewModel.Drinks.Items);
        }

        [HttpGet]
        public async Task<IActionResult> Index(AdminDrinkFilterDTO filter)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.DrinkView, false);
            if (guard != null) return guard;
            var viewModel = await _drinkService.GetIndexDataAsync(filter);

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.DrinkCreate, false);
            if (guard != null) return guard;
            var categories = await _drinkService.GetDrinkCategoriesAsync();

            var productTypes = await _drinkService.GetProductTypesAsync();

            var viewModel =
                new AdminDrinkCreateViewModel
                {
                    DrinkCreateDTO = new AdminDrinkCreateDTO(),

                    Categories =
                        categories.Select(c =>
                            new SelectListItem
                            {
                                Value = c.CategoryId.ToString(),

                                Text = c.Name
                            }),

                    ProductTypes =
                        productTypes.Select(pt =>
                            new SelectListItem
                            {
                                Value = pt.ProductTypeId.ToString(),

                                Text = pt.Name
                            })
                };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            AdminDrinkCreateViewModel viewModel)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.DrinkCreate);
            if (guard != null) return guard;
            ModelState.Remove("DrinkCreateDTO.ImageFiles");

            ModelState.Remove(nameof(viewModel.Categories));

            ModelState.Remove(nameof(viewModel.ProductTypes));

            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Dữ liệu không hợp lệ"
                });
            }

            try
            {
                await _drinkService.CreateDrinkAsync(viewModel.DrinkCreateDTO);

                return Json(new
                {
                    success = true,
                    message = "Thêm đồ uống thành công!",
                    redirectUrl = Url.Action("Index")
                });
            }
            catch (ArgumentException ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception)
            {
                return Json(new
                {
                    success = false,
                    message = "Đã có lỗi xảy ra"
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.DrinkUpdate, false);
            if (guard != null) return guard;
            var updateDTO = await _drinkService.GetDrinkForUpdateAsync(id);

            if (updateDTO == null)
            {
                return NotFound();
            }

            var categories = await _drinkService.GetDrinkCategoriesAsync();

            var productTypes = await _drinkService.GetProductTypesAsync();

            var viewModel =
                new AdminDrinkEditViewModel
                {
                    DrinkUpdateDTO = updateDTO,

                    Categories =
                        categories.Select(c =>
                            new SelectListItem
                            {
                                Value = c.CategoryId.ToString(),

                                Text = c.Name
                            }),

                    ProductTypes =
                        productTypes.Select(pt =>
                            new SelectListItem
                            {
                                Value = pt.ProductTypeId.ToString(),

                                Text = pt.Name
                            })
                };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminDrinkEditViewModel viewModel)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.DrinkUpdate);
            if (guard != null) return guard;
            ModelState.Remove(nameof(viewModel.Categories));

            ModelState.Remove(nameof(viewModel.ProductTypes));

            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Dữ liệu không hợp lệ"
                });
            }

            try
            {
                await _drinkService.UpdateDrinkAsync(viewModel.DrinkUpdateDTO);

                return Json(new
                {
                    success = true,
                    message = "Cập nhật đồ uống thành công!",
                    redirectUrl = Url.Action("Index")
                });
            }
            catch (ArgumentException ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception)
            {
                return Json(new
                {
                    success = false,
                    message = "Đã có lỗi xảy ra"
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.DrinkToggleStatus);
            if (guard != null) return guard;
            try
            {
                await _drinkService.ToggleDrinkStatusAsync(id);

                return Json(new
                {
                    success = true,
                    message = "Đã cập nhật trạng thái đồ uống thành công."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message =
                        "Lỗi khi cập nhật trạng thái: " +
                        ex.Message
                });
            }
        }

        // =====================================================
        // Drink Images
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> GetImages(int drinkId)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.DrinkUpdateImage);
            if (guard != null) return guard;
            var images =  await _drinkService.GetDrinkImagesAsync(drinkId);

            return Json(new
            {
                success = true,
                data = images
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadImage(int drinkId, IFormFile imageFile, bool isDefault)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.DrinkUpdateImage);
            if (guard != null) return guard;
            try
            {
                await _drinkService.AddDrinkImageAsync(drinkId, imageFile, isDefault);

                return Json(new
                {
                    success = true,
                    message = "Thêm ảnh thành công."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefaultImage(int drinkId, int drinkImageId)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.DrinkUpdateImage);
            if (guard != null) return guard;
            try
            {
                await _drinkService.SetDefaultDrinkImageAsync(drinkId, drinkImageId);

                return Json(new
                {
                    success = true,
                    message = "Đã cập nhật ảnh mặc định."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(int drinkImageId)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.DrinkUpdateImage);
            if (guard != null) return guard;
            try
            {
                await _drinkService.DeleteDrinkImageAsync(drinkImageId);

                return Json(new
                {
                    success = true,
                    message = "Đã xóa ảnh thành công."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateImage(int drinkImageId, IFormFile newImageFile)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.DrinkUpdateImage);
            if (guard != null) return guard;
            try
            {
                await _drinkService.UpdateDrinkImageAsync(drinkImageId, newImageFile);

                return Json(new
                {
                    success = true,
                    message = "Cập nhật ảnh thành công."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiSuggestion([FromBody] DrinkSuggestionRequestDTO request)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.DrinkCreate);
            if (guard != null) return guard;
            if (!ModelState.IsValid)
                return ApiError("Thông tin gợi ý không hợp lệ.", StatusCodes.Status400BadRequest);

            var result = await _aiService.SuggestDrinkAsync(request, HttpContext.RequestAborted);
            if (!result.Success) Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new
            {
                success = result.Success,
                message = result.Message,
                requestId = result.RequestId,
                options = result.Options,
                warnings = result.Warnings,
                usedOllama = result.UsedOllama,
                usedFallback = result.UsedFallback
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiReferenceImages([FromBody] AIReferenceSearchRequestDTO request)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.DrinkCreate);
            if (guard != null) return guard;
            if (!ModelState.IsValid)
                return ApiError("Yêu cầu tìm ảnh tham chiếu không hợp lệ.", StatusCodes.Status400BadRequest);
            request.EntityType = "Drink";
            var result = await _aiImagePipeline.SearchReferenceImagesAsync(request, HttpContext.RequestAborted);
            if (!result.Success) Response.StatusCode = result.Retryable
                ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status422UnprocessableEntity;
            return Json(new { success = result.Success, message = result.Message, data = result });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiGenerateFromReference([FromBody] AIGenerateFromReferenceRequestDTO request)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.DrinkCreate);
            if (guard != null) return guard;
            if (!ModelState.IsValid)
                return ApiError("Yêu cầu tạo ảnh không hợp lệ.", StatusCodes.Status400BadRequest);
            request.EntityType = "Drink";
            var result = await _aiImagePipeline.GenerateFromReferenceAsync(request, HttpContext.RequestAborted);
            if (!result.Success) Response.StatusCode = result.Retryable
                ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status422UnprocessableEntity;
            return Json(new { success = result.Success, message = result.Message, data = result });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiUsePexelsImage([FromBody] AIUsePexelsImageRequestDTO request)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.DrinkCreate);
            if (guard != null) return guard;
            if (!ModelState.IsValid)
                return ApiError("Yêu cầu dùng ảnh Pexels không hợp lệ.", StatusCodes.Status400BadRequest);
            request.EntityType = "Drink";
            var result = await _aiImagePipeline.UsePexelsImageAsync(request, HttpContext.RequestAborted);
            if (!result.Success) Response.StatusCode = result.Retryable
                ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status422UnprocessableEntity;
            return Json(new { success = result.Success, message = result.Message, data = result });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiGenerateWithoutReference([FromBody] AIGenerateFromPromptRequestDTO request)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.DrinkCreate);
            if (guard != null) return guard;
            if (!ModelState.IsValid)
                return ApiError("Yêu cầu tạo ảnh không dùng Pexels không hợp lệ.", StatusCodes.Status400BadRequest);
            request.EntityType = "Drink";
            var result = await _aiImagePipeline.GenerateFromPromptAsync(request, HttpContext.RequestAborted);
            if (!result.Success) Response.StatusCode = result.Retryable
                ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status422UnprocessableEntity;
            return Json(new { success = result.Success, message = result.Message, data = result });
        }

        private async Task<IActionResult?> EnsurePermissionAsync(string permissionCode, bool jsonResponse = true)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId))
                return jsonResponse ? ApiError("Unauthorized.", StatusCodes.Status401Unauthorized)
                    : RedirectToAction("AccessDenied", "Account", new { area = "" });
            var decision = await _permissionService.HasPermissionAsync(accountId, permissionCode);
            if (!decision.IsSuccess || decision.Data?.Allowed != true)
                return jsonResponse ? ApiError("Bạn không có quyền thực hiện chức năng này.", StatusCodes.Status403Forbidden)
                    : RedirectToAction("AccessDenied", "Account", new { area = "" });
            return null;
        }

        private JsonResult ApiError(string message, int statusCode)
        {
            Response.StatusCode = statusCode;
            return Json(new { success = false, message });
        }
    }
}
