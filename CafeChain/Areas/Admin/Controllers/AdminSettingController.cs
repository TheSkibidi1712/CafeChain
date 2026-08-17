using System.Collections.Generic;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.Authorization;
using CafeChain.Application.DTOs.Admin.Settings;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.Settings;
using CafeChain.ViewModels.Admin.Settings;
using CafeChain.Application.Services.AIImport;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    [RequirePermission(PermissionConstants.SettingsView)]
    public class AdminSettingController : AdminBaseController
    {
        private readonly IAdminSettingService _settingService;
        private readonly IAdminActorContextAccessor _actorContextAccessor;
        private readonly IAIImportOcrRuntimeSettings _ocrSettings;

        public AdminSettingController(
            IAdminSettingService settingService,
            IAdminActorContextAccessor actorContextAccessor,
            IAIImportOcrRuntimeSettings ocrSettings)
        {
            _settingService = settingService;
            _actorContextAccessor = actorContextAccessor;
            _ocrSettings = ocrSettings;
        }

        public async Task<IActionResult> Index(string? tab = null)
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
            var ocr = await _ocrSettings.GetAsync(HttpContext.RequestAborted);

            return View(new AdminSettingIndexVM
            {
                NegativeInventory = negativeInventory,
                Ocr = ToDto(ocr),
                ActiveTab = string.Equals(tab, "ocr", StringComparison.OrdinalIgnoreCase) ? "ocr" : "negative-stock"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.SettingsUpdate)]
        public async Task<IActionResult> UpdateOcr(UpdateAIImportOcrSettingsDTO request)
        {
            if (!ModelState.IsValid) return UnprocessableEntity(new { success = false, errorCode = "OCR_SETTING_VALIDATION", message = "Dữ liệu cấu hình OCR không hợp lệ." });
            try
            {
                var state = await _ocrSettings.UpdateAsync(new AIImportOcrRuntimeUpdate(
                    request.Languages, request.ReviewConfidenceThreshold, request.RenderDpi,
                    request.MaxPages, request.MaxRenderedPixelsPerPage, request.MaxTotalRenderedPixels,
                    request.PageTimeoutSeconds, request.TotalTimeoutSeconds, request.MaxConcurrentPages),
                    _actorContextAccessor.Get(User), HttpContext.RequestAborted);
                var message = state.ProviderReady && state.HealthStatus == "READY"
                    ? "Đã lưu cấu hình và Tesseract OCR sẵn sàng."
                    : $"Đã lưu cấu hình nhưng OCR chưa sẵn sàng. {state.HealthMessage}";
                return Ok(new { success = true, message, data = ToDto(state) });
            }
            catch (ArgumentException exception)
            {
                return UnprocessableEntity(new { success = false, errorCode = "OCR_SETTING_VALIDATION", message = exception.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.SettingsUpdate)]
        public async Task<IActionResult> CheckOcr()
        {
            var state = await _ocrSettings.CheckHealthAsync(_actorContextAccessor.Get(User), HttpContext.RequestAborted);
            var payload = new { success = state.ProviderReady && state.HealthStatus == "READY", message = state.HealthMessage, data = ToDto(state) };
            return payload.success ? Ok(payload) : StatusCode(StatusCodes.Status503ServiceUnavailable, payload);
        }

        private static AIImportOcrSettingsDTO ToDto(AIImportOcrRuntimeState state) => new()
        {
            InfrastructureConfigured = state.InfrastructureConfigured,
            ProviderReady = state.ProviderReady,
            EffectiveEnabled = state.EffectiveEnabled,
            Provider = state.Provider,
            ProviderVersion = state.ProviderVersion,
            Languages = state.Languages,
            ExecutableAvailable = state.ExecutableAvailable,
            ModelDataReady = state.ModelDataReady,
            ReviewConfidenceThreshold = state.ReviewConfidenceThreshold,
            RenderDpi = state.RenderDpi,
            MaxPages = state.MaxPages,
            MaxRenderedPixelsPerPage = state.MaxRenderedPixelsPerPage,
            MaxTotalRenderedPixels = state.MaxTotalRenderedPixels,
            PageTimeoutSeconds = state.PageTimeoutSeconds,
            TotalTimeoutSeconds = state.TotalTimeoutSeconds,
            MaxConcurrentPages = state.MaxConcurrentPages,
            ConfigVersion = state.ConfigVersion,
            HealthStatus = state.HealthStatus,
            HealthMessage = state.HealthMessage,
            LastHealthCheckedAtUtc = state.LastHealthCheckedAtUtc
        };

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
