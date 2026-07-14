using CafeChain.Application.DTOs.Admin.DrinkToppings;
using CafeChain.Application.DTOs.Admin.Toppings;
using CafeChain.Application.Interfaces.Admin.DrinkToppings;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Interfaces.Admin.Toppings;
using CafeChain.ViewModels.Admin.DrinkToppings;
using CafeChain.ViewModels.Admin.Toppings;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.AI;

namespace CafeChain.Areas.Admin.Controllers
{
    public class AdminToppingController : AdminBaseController
    {
        private readonly IAdminToppingService _toppingService;
        private readonly IAdminDrinkToppingService _drinkToppingService;
        private readonly IAdminPermissionService _permissionService;
        private readonly IAIService _aiService;
        private readonly IAIImagePipelineService _aiImagePipeline;
        private readonly IAdminRecipeQueryService _recipeQueryService;

        public AdminToppingController(
            IAdminToppingService toppingService,
            IAdminDrinkToppingService drinkToppingService,
            IAdminPermissionService permissionService,
            IAIService aiService,
            IAIImagePipelineService aiImagePipeline,
            IAdminRecipeQueryService recipeQueryService)
        {
            _toppingService = toppingService;
            _drinkToppingService = drinkToppingService;
            _permissionService = permissionService;
            _aiService = aiService;
            _aiImagePipeline = aiImagePipeline;
            _recipeQueryService = recipeQueryService;
        }


        // =====================================================
        // INDEX
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.ToppingView, false);
            if (guard != null) return guard;
            var toppings = (await _toppingService.GetAllAsync()).ToList();
            var sources = await _recipeQueryService.GetToppingConsumptionSourcesAsync(
                toppings.Select(x => x.ToppingId));

            var vm = toppings.Select(x => new AdminToppingVM
            {
                ToppingId = x.ToppingId,
                ToppingCode = x.ToppingCode,
                Name = x.Name,
                Price = x.Price,
                ImageUrl = x.ImageUrl,
                Active = x.Active,
                ConsumptionSource = sources[x.ToppingId]
            }).ToList();

            return View(vm);
        }

        // =====================================================
        // CREATE TOPPING
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            AdminToppingVM vm)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.ToppingCreate);
            if (guard != null) return guard;
            try
            {
                if (!ModelState.IsValid)
                {
                    return Json(new
                    {
                        success = false,
                        message = GetModelStateError()
                    });
                }

                await _toppingService.CreateAsync(
                    MapToDto(vm));

                return Json(new
                {
                    success = true,
                    message = "Thêm topping thành công"
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

        // =====================================================
        // UPDATE TOPPING
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            AdminToppingVM vm)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.ToppingUpdate);
            if (guard != null) return guard;
            try
            {
                if (!ModelState.IsValid)
                {
                    return Json(new
                    {
                        success = false,
                        message = GetModelStateError()
                    });
                }

                await _toppingService.UpdateAsync(
                    MapToDto(vm));

                return Json(new
                {
                    success = true,
                    message = "Cập nhật topping thành công"
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

        // =====================================================
        // TOGGLE TOPPING STATUS
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(
            int id)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.ToppingToggleStatus);
            if (guard != null) return guard;
            try
            {
                await _toppingService
                    .ToggleStatusAsync(id);

                return Json(new
                {
                    success = true,
                    message = "Cập nhật trạng thái thành công"
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

        // =====================================================
        // GET DRINKS OF TOPPING
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> GetDrinks(int toppingId)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.ToppingAssignDrink);
            if (guard != null) return guard;
            try
            {
                var result =
                    await _drinkToppingService
                        .GetDrinksForToppingAsync(toppingId);

                return Json(new
                {
                    success = true,
                    data = result
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

        // =====================================================
        // ASSIGN TOPPING TO DRINK
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign([FromBody] AssignDrinkToppingVM vm)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.ToppingAssignDrink);
            if (guard != null) return guard;
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = GetModelStateError()
                });
            }

            try
            {
                var dto = new DrinkToppingDto
                {
                    DrinkId = vm.DrinkId,
                    ToppingId = vm.ToppingId
                };

                await _drinkToppingService.AssignAsync(dto);

                return Json(new
                {
                    success = true,
                    message = "Gán topping thành công"
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

        // =====================================================
        // TOGGLE DRINK TOPPING
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.ToppingAssignDrink);
            if (guard != null) return guard;
            try
            {
                if (id <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Id không hợp lệ"
                    });
                }

                await _drinkToppingService.ToggleAsync(id);

                return Json(new
                {
                    success = true,
                    message = "Cập nhật trạng thái thành công"
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

        // =====================================================
        // PRIVATE HELPERS
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiSuggestion([FromBody] ToppingSuggestionRequestDTO request)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.ToppingCreate);
            if (guard != null) return guard;
            if (!ModelState.IsValid)
                return ApiError("Thông tin gợi ý không hợp lệ.", StatusCodes.Status400BadRequest);
            var result = await _aiService.SuggestToppingAsync(request, HttpContext.RequestAborted);
            if (!result.Success) Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { success = result.Success, message = result.Message, requestId = result.RequestId, options = result.Options,
                warnings = result.Warnings,
                usedOllama = result.UsedOllama, usedFallback = result.UsedFallback });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiReferenceImages([FromBody] AIReferenceSearchRequestDTO request)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.ToppingCreate);
            if (guard != null) return guard;
            if (!ModelState.IsValid)
                return ApiError("Yêu cầu tìm ảnh tham chiếu không hợp lệ.", StatusCodes.Status400BadRequest);
            request.EntityType = "Topping";
            var result = await _aiImagePipeline.SearchReferenceImagesAsync(request, HttpContext.RequestAborted);
            if (!result.Success) Response.StatusCode = result.Retryable
                ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status422UnprocessableEntity;
            return Json(new { success = result.Success, message = result.Message, data = result });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiGenerateFromReference([FromBody] AIGenerateFromReferenceRequestDTO request)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.ToppingCreate);
            if (guard != null) return guard;
            if (!ModelState.IsValid)
                return ApiError("Yêu cầu tạo ảnh không hợp lệ.", StatusCodes.Status400BadRequest);
            request.EntityType = "Topping";
            var result = await _aiImagePipeline.GenerateFromReferenceAsync(request, HttpContext.RequestAborted);
            if (!result.Success) Response.StatusCode = result.Retryable
                ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status422UnprocessableEntity;
            return Json(new { success = result.Success, message = result.Message, data = result });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiUsePexelsImage([FromBody] AIUsePexelsImageRequestDTO request)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.ToppingCreate);
            if (guard != null) return guard;
            if (!ModelState.IsValid)
                return ApiError("Yêu cầu dùng ảnh Pexels không hợp lệ.", StatusCodes.Status400BadRequest);
            request.EntityType = "Topping";
            var result = await _aiImagePipeline.UsePexelsImageAsync(request, HttpContext.RequestAborted);
            if (!result.Success) Response.StatusCode = result.Retryable
                ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status422UnprocessableEntity;
            return Json(new { success = result.Success, message = result.Message, data = result });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiGenerateWithoutReference([FromBody] AIGenerateFromPromptRequestDTO request)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.ToppingCreate);
            if (guard != null) return guard;
            if (!ModelState.IsValid)
                return ApiError("Yêu cầu tạo ảnh không dùng Pexels không hợp lệ.", StatusCodes.Status400BadRequest);
            request.EntityType = "Topping";
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

        private static ToppingDto MapToDto(AdminToppingVM vm)
        {
            return new ToppingDto
            {
                ToppingId = vm.ToppingId,
                ToppingCode = vm.ToppingCode,
                Name = vm.Name,
                Price = vm.Price,
                ImageFile = vm.ImageFile
            };
        }

        private string GetModelStateError()
        {
            return ModelState.Values
                .SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage)
                .FirstOrDefault()
                ?? "Dữ liệu không hợp lệ";
        }
    }
}
