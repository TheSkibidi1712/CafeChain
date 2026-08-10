using System.Collections.Generic;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.Authorization;
using CafeChain.Application.DTOs.Admin.Settings;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.Settings;
using CafeChain.ViewModels.Admin.Settings;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RequirePermission(PermissionConstants.SettingsView)]
    public class AdminSettingController : AdminBaseController
    {
        private readonly IAdminSettingService _settingService;
        private readonly IAdminActorContextAccessor _actorContextAccessor;

        public AdminSettingController(
            IAdminSettingService settingService,
            IAdminActorContextAccessor actorContextAccessor)
        {
            _settingService = settingService;
            _actorContextAccessor = actorContextAccessor;
        }

        public async Task<IActionResult> Index()
        {
            var actor = _actorContextAccessor.Get(User);
            var negativeResult = await _settingService.GetNegativeInventorySettingsAsync(actor, HttpContext.RequestAborted);
            var negativeInventory = negativeResult.IsSuccess
                ? negativeResult.Data
                : new NegativeInventorySettingsDTO
                {
                    IsConfigurationValid = false,
                    ConfigurationError = negativeResult.Message
                };

            return View(new AdminSettingIndexVM
            {
                NegativeInventory = negativeInventory
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.SettingsUpdate)]
        public async Task<IActionResult> UpdateNegativeInventory(UpdateNegativeInventorySettingsDTO request)
        {
            if (!ModelState.IsValid)
            {
                return UnprocessableEntity(new
                {
                    success = false,
                    errorCode = "NEGATIVE_INVENTORY_SETTING_VALIDATION",
                    message = "Dữ liệu cấu hình âm kho không hợp lệ.",
                    errors = ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)
                });
            }

            var actor = _actorContextAccessor.Get(User);
            var result = await _settingService.UpdateNegativeInventorySettingsAsync(
                request,
                actor,
                HttpContext.RequestAborted);

            if (result.IsSuccess)
            {
                return Ok(new
                {
                    success = true,
                    message = result.Message,
                    changed = result.Data.Changed,
                    policyVersion = result.Data.PolicyVersion
                });
            }

            var payload = new
            {
                success = false,
                result.ErrorCode,
                message = result.Message,
                result.Errors
            };
            return result.ErrorCode switch
            {
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, payload),
                "NEGATIVE_INVENTORY_SETTING_STALE" => Conflict(payload),
                "NEGATIVE_SETTING_INVALID" => UnprocessableEntity(payload),
                "NEGATIVE_INVENTORY_SETTING_VALIDATION" => UnprocessableEntity(payload),
                _ => StatusCode(StatusCodes.Status500InternalServerError, payload)
            };
        }
    }
}
