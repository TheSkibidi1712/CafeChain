using CafeChain.Application.DTOs.Admin.DrinkSizes;
using CafeChain.Application.DTOs.Admin.Sizes;
using CafeChain.Application.Interfaces.Admin.DrinkSizes;
using CafeChain.Application.Interfaces.Admin.Sizes;
using CafeChain.ViewModels.Admin.DrinkSizes;
using CafeChain.ViewModels.Admin.Sizes;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.AI;

namespace CafeChain.Areas.Admin.Controllers
{
    public class AdminSizeController : AdminBaseController
    {
        private readonly IAdminSizeService _adminsizeService;
        private readonly IAdminDrinkSizeService _drinkSizeService;
        private readonly IAdminPermissionService _permissionService;
        private readonly IAIService _aiService;

        public AdminSizeController(
            IAdminSizeService adminsizeService,
            IAdminDrinkSizeService adminDrinkSizeService,
            IAdminPermissionService permissionService,
            IAIService aiService)
        {
            _adminsizeService = adminsizeService;
            _drinkSizeService = adminDrinkSizeService;
            _permissionService = permissionService;
            _aiService = aiService;
        }

        public async Task<IActionResult> Index()
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.SizeView, false);
            if (guard != null) return guard;
            var sizes = await _adminsizeService.GetActiveSizesAsync();

            var vm = sizes.Select(s => new AdminSizeVM
            {
                SizeId = s.SizeId,
                SizeCode = s.SizeCode,
                Name = s.Name,
                Description = s.Description,
                SizeType = s.SizeType,
                Active = s.Active
            });

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] AdminSizeVM vm)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.SizeCreate);
            if (guard != null) return guard;
            if (vm == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Dữ liệu không hợp lệ"
                });
            }

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
                var result = await _adminsizeService.CreateSizeAsync(new SizeDto
                {
                    SizeCode = vm.SizeCode,
                    Name = vm.Name,
                    Description = vm.Description,
                    SizeType = vm.SizeType
                });

                if (!result.Success)
                {
                    return Json(new
                    {
                        success = false,
                        message = result.Error
                    });
                }

                return Json(new
                {
                    success = true,
                    message = "Tạo size thành công"
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
        public async Task<IActionResult> Edit([FromBody] AdminSizeVM vm)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.SizeUpdate);
            if (guard != null) return guard;
            if (vm == null || vm.SizeId <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "Không tìm thấy size"
                });
            }

            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = GetModelStateError()
                });
            }

            var result = await _adminsizeService.UpdateSizeAsync(new SizeDto
            {
                SizeId = vm.SizeId,
                SizeCode = vm.SizeCode,
                Name = vm.Name,
                Description = vm.Description,
                SizeType = vm.SizeType
            });

            if (!result.Success)
            {
                return Json(new
                {
                    success = false,
                    message = result.Error
                });
            }

            return Json(new
            {
                success = true,
                message = "Cập nhật size thành công"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.SizeToggleStatus);
            if (guard != null) return guard;
            try
            {
                await _adminsizeService.ToggleStatusAsync(id);

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

        [HttpGet]
        public async Task<IActionResult> GetDrinks(int sizeId)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.SizeAssignDrink);
            if (guard != null) return guard;
            var data = await _drinkSizeService.GetDrinksForSizeAsync(sizeId);
            return Json(data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignDrink([FromBody] AssignDrinkSizeVM vm)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.SizeAssignDrink);
            if (guard != null) return guard;
            if (!ModelState.IsValid)
                return BadRequest("Invalid data");

            try
            {
                await _drinkSizeService.AssignDrinkAsync(new DrinkSizeDto
                {
                    DrinkId = vm.DrinkId,
                    SizeId = vm.SizeId,
                    Price = vm.Price
                });

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleDrinkSize(int id)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.SizeAssignDrink);
            if (guard != null) return guard;
            try
            {
                await _drinkSizeService.ToggleDrinkSizeAsync(id);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePrice(int drinkSizeId, decimal price)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.SizeAssignDrink);
            if (guard != null) return guard;
            if (price <= 0)
                return BadRequest("Giá không hợp lệ");

            await _drinkSizeService.UpdatePriceAsync(new DrinkSizeDto
            {
                DrinkSizeId = drinkSizeId,
                Price = price
            });

            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiSuggestion([FromBody] SizeSuggestionRequestDTO request)
        {
            var guard = await EnsurePermissionAsync(PermissionConstants.SizeCreate);
            if (guard != null) return guard;
            if (!ModelState.IsValid)
                return ApiError("Thông tin gợi ý không hợp lệ.", StatusCodes.Status400BadRequest);
            var result = await _aiService.SuggestSizeAsync(request, HttpContext.RequestAborted);
            if (!result.Success) Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { success = result.Success, message = result.Message, options = result.Options,
                warnings = result.Warnings,
                usedOllama = result.UsedOllama, usedFallback = result.UsedFallback });
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
