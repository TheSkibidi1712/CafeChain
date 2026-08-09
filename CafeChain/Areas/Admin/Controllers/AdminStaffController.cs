using CafeChain.Application.Interfaces.Admin.Staffs;
using CafeChain.Application.Authorization;
using CafeChain.Application.Constants;
using CafeChain.Application.Results;
using CafeChain.ViewModels.Admin.Staffs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Security.Claims;

namespace CafeChain.Areas.Admin.Controllers
{
    public class AdminStaffController : AdminBaseController
    {
        private readonly IAdminStaffService _staffService;

        public AdminStaffController(IAdminStaffService staffService)
        {
            _staffService = staffService;
        }

        // ==================== HELPER: Gom logic đọc Claims ====================
        private int? GetCurrentManagerStoreId()
        {
            if (User.IsInRole(RoleConstants.StoreManager)
                && !User.IsInRole(RoleConstants.BusinessOwner)
                && !User.IsInRole(RoleConstants.SystemAdmin))
            {
                var storeIdClaim = User.FindFirst("StoreId")?.Value;
                if (int.TryParse(storeIdClaim, out int sid))
                    return sid;
            }
            return null;
        }

        private async Task SetViewBagFromMasterData(ClaimsPrincipal user)
        {
            var masterData = await _staffService.GetMasterDataForFormAsync(user);
            ViewBag.Roles = masterData.Roles;
            ViewBag.Stores = masterData.Stores;
            ViewBag.ScopeTypes = masterData.ScopeTypes;
            ViewBag.Provinces = masterData.Provinces;
            ViewBag.IsStoreManager = masterData.IsStoreManager;
            ViewBag.CurrentStoreId = masterData.CurrentStoreId;
            ViewBag.CurrentStoreName = masterData.CurrentStoreName;
        }

        // ==================== INDEX ====================
        [RequirePermission(PermissionConstants.StaffView)]
        public async Task<IActionResult> Index(int page = 1, string search = "", int? roleFilter = null)
        {
            var storeId = GetCurrentManagerStoreId();
            var model = await _staffService.GetStaffIndexPageAsync(page, 6, storeId, search, roleFilter, User);
            await SetViewBagFromMasterData(User);
            return View(model);
        }

        // ==================== CREATE (POST) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.StaffCreate)]
        public async Task<IActionResult> Create(StaffCreateVM model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage)
                        ? "Dữ liệu không hợp lệ."
                        : e.ErrorMessage)
                    .Distinct()
                    .ToArray();
                if (IsAjaxRequest())
                {
                    return UnprocessableEntity(new
                    {
                        success = false,
                        message = "Dữ liệu tạo nhân viên chưa hợp lệ.",
                        errors
                    });
                }

                TempData["Error"] = "Dữ liệu không hợp lệ: " + string.Join("; ", errors);
                return RedirectToAction(nameof(Index));
            }

            ServiceResult result;
            try
            {
                result = await _staffService.CreateStaffAsync(model, User, model.AvatarFile);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }

            if (!result.IsSuccess)
            {
                if (IsAjaxRequest())
                {
                    return UnprocessableEntity(new
                    {
                        success = false,
                        message = result.Message
                    });
                }

                TempData["Error"] = result.Message;
                return RedirectToAction(nameof(Index));
            }

            if (IsAjaxRequest())
            {
                return Json(new
                {
                    success = true,
                    message = result.Message,
                    redirectUrl = Url.Action("Index", "AdminPermission", new
                    {
                        area = "Admin",
                        staffId = result.EntityId
                    })
                });
            }

            TempData["Success"] = result.Message;
            return RedirectToAction("Index", "AdminPermission", new { area = "Admin", staffId = result.EntityId });
        }

        private bool IsAjaxRequest() =>
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        // ==================== EDIT (GET) ====================
        [RequirePermission(PermissionConstants.StaffUpdate)]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var model = await _staffService.GetStaffForEditAsync(id, User);
                if (model == null) return NotFound();

                await SetViewBagFromMasterData(User);
                return View(model);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        // ==================== EDIT (POST) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.StaffUpdate)]
        public async Task<IActionResult> Edit(int id, StaffEditVM model)
        {
            if (id != model.StaffId) return BadRequest();

            // Form posts "" for optional value-types (DateTime?/int?/cleared decimals) and
            // sometimes for Scope when UI placeholder is selected — binder yields
            // "The value '' is invalid." Normalize before ModelState validity check.
            NormalizeEmptyOptionalBindings(ModelState, model);

            if (!ModelState.IsValid)
            {
                var errors = string.Join("<br/>", ModelState
                    .Where(kvp => kvp.Value?.Errors.Count > 0)
                    .SelectMany(kvp => kvp.Value!.Errors.Select(err =>
                        FormatModelStateError(kvp.Key, err.ErrorMessage))));
                TempData["Error"] = "Cập nhật thất bại:<br/>" + string.Join("<br/>", errors.Distinct());
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var result = await _staffService.UpdateStaffAsync(model, User, model.AvatarFile);

                if (!result.IsSuccess)
                {
                    TempData["Error"] = result.Message;
                    await SetViewBagFromMasterData(User);
                    return View(model);
                }

                TempData["Success"] = result.Message;
                return RedirectToAction(nameof(Index));
            }
            catch (UnauthorizedAccessException ex)
            {
                // Xử lý ném exception từ Service theo đúng yêu cầu
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>
        /// ASP.NET Core model binder rejects empty string for nullable value types and non-nullable ints
        /// with the English message "The value '' is invalid." Optional fields must treat "" as null/0.
        /// </summary>
        private static void NormalizeEmptyOptionalBindings(ModelStateDictionary modelState, StaffEditVM model)
        {
            // Optional nullables: empty string → null
            if (IsEmptyInvalidBinding(modelState, nameof(StaffEditVM.DateOfBirth)))
            {
                model.DateOfBirth = null;
                modelState.Remove(nameof(StaffEditVM.DateOfBirth));
            }

            if (IsEmptyInvalidBinding(modelState, nameof(StaffEditVM.StartDate)))
            {
                model.StartDate = null;
                modelState.Remove(nameof(StaffEditVM.StartDate));
            }

            if (IsEmptyInvalidBinding(modelState, nameof(StaffEditVM.StoreId)))
            {
                model.StoreId = null;
                modelState.Remove(nameof(StaffEditVM.StoreId));
            }

            // Cleared number inputs → 0

            // Nested Dependents[i].DateOfBirth empty
            foreach (var key in modelState.Keys
                         .Where(k => k.Contains("DateOfBirth", StringComparison.OrdinalIgnoreCase))
                         .ToList())
            {
                if (IsEmptyInvalidBinding(modelState, key))
                    modelState.Remove(key);
            }

            // Empty optional BHYT fails MinimumLength(10); treat blank as null.

            // Empty string optional text already bound; trim blanks on CCCD/TaxCode left to service.
        }

        private static bool IsEmptyInvalidBinding(ModelStateDictionary modelState, string key)
        {
            if (!modelState.TryGetValue(key, out var entry) || entry == null)
                return false;

            return entry.Errors.Any(e =>
            {
                var msg = e.ErrorMessage ?? string.Empty;
                return msg.Contains("The value '' is invalid", StringComparison.OrdinalIgnoreCase)
                       || msg.Contains("The value '' is invalid.", StringComparison.OrdinalIgnoreCase)
                       || (msg.Contains("is invalid", StringComparison.OrdinalIgnoreCase) && msg.Contains("''"));
            });
        }

        private static string FormatModelStateError(string key, string? rawMessage)
        {
            var message = rawMessage?.Trim() ?? string.Empty;
            var isEmptyInvalid =
                message.Contains("The value '' is invalid", StringComparison.OrdinalIgnoreCase)
                || (message.Contains("is invalid", StringComparison.OrdinalIgnoreCase) && message.Contains("''"));

            var field = key.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? key;

            if (isEmptyInvalid || string.IsNullOrEmpty(message))
            {
                return field switch
                {
                    nameof(StaffEditVM.SelectedRoleId) => "Vui lòng chọn vai trò.",
                    nameof(StaffEditVM.ScopeTypeId) => "Vui lòng chọn phạm vi quản lý.",
                    nameof(StaffEditVM.ScopeRefId) => "Vui lòng chọn phạm vi / cửa hàng tham chiếu.",
                    nameof(StaffEditVM.StoreId) => "Vui lòng chọn cửa hàng.",
                    nameof(StaffEditVM.DateOfBirth) => "Ngày sinh không hợp lệ.",
                    _ => "Giá trị không hợp lệ."
                };
            }

            // Already localized attribute messages
            if (message.Any(c => c >= 0x00C0))
                return message;

            return field switch
            {
                nameof(StaffEditVM.SelectedRoleId) => "Vui lòng chọn vai trò.",
                nameof(StaffEditVM.ScopeTypeId) => "Vui lòng chọn phạm vi quản lý.",
                nameof(StaffEditVM.ScopeRefId) => "Vui lòng chọn phạm vi / cửa hàng tham chiếu.",
                nameof(StaffEditVM.StoreId) => "Vui lòng chọn cửa hàng.",
                nameof(StaffEditVM.Email) => string.IsNullOrWhiteSpace(message) ? "Email không hợp lệ." : message,
                _ => string.IsNullOrWhiteSpace(message) ? "Giá trị không hợp lệ." : message
            };
        }

        // ==================== TOGGLE STATUS (AJAX) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.StaffToggleStatus)]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                var result = await _staffService.ToggleStaffStatusAsync(id, User);
                return Json(new { success = result.IsSuccess, message = result.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        
        // ==================== MANUAL PASSWORD RESET (AJAX) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission(PermissionConstants.StaffResetPassword)]
        public async Task<IActionResult> ManualResetPassword([FromBody] ManualResetRequest req)
        {
            var result = await _staffService.ResetPasswordAsync(req.AccountId, req.NewPassword, User);
            return Json(new { success = result.IsSuccess, message = result.Message });
        }

        // ==================== DYNAMIC DROPDOWN API ====================
        [HttpGet]
        [RequirePermission(PermissionConstants.StaffCreate)]
        public async Task<IActionResult> GetScopeReferences(int scopeTypeId, int? parentId = null)
        {
            try
            {
                var data = await _staffService.GetScopeReferencesAsync(scopeTypeId, User, parentId);
                return Json(data);
            }
            catch
            {
                return Json(new object[] { });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetWards(int provinceId)
        {
            return Json(await _staffService.GetWardsAsync(provinceId));
        }
    }

    public class ManualResetRequest {
        public int AccountId { get; set; }
        public string NewPassword { get; set; }
    }
}

