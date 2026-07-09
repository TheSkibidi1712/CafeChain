using CafeChain.Application.Interfaces.Admin.Staffs;
using CafeChain.ViewModels.Admin.Staffs;
using CafeChain.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Areas.Admin.Controllers
{
    public class AdminStaffController : AdminBaseController
    {
        private readonly IAdminStaffService _staffService;
        private readonly AppDbContext _dbContext;

        public AdminStaffController(IAdminStaffService staffService, AppDbContext dbContext)
        {
            _staffService = staffService;
            _dbContext = dbContext;
        }

        // ==================== HELPER: Gom logic đọc Claims ====================
        private int? GetCurrentManagerStoreId()
        {
            if (User.IsInRole("Store Manager") && !User.IsInRole("Admin System"))
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
            ViewBag.IsStoreManager = masterData.IsStoreManager;
            ViewBag.CurrentStoreId = masterData.CurrentStoreId;
            ViewBag.CurrentStoreName = masterData.CurrentStoreName;
        }

        // ==================== INDEX ====================
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
        public async Task<IActionResult> Create(StaffCreateVM model)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join("<br/>", ModelState.Values
                                        .SelectMany(v => v.Errors)
                                        .Select(e => e.ErrorMessage));
                TempData["Error"] = "Dữ liệu không hợp lệ:<br/>" + errors;
                return RedirectToAction(nameof(Index));
            }

            var result = await _staffService.CreateStaffAsync(model, User, model.AvatarFile);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // ==================== EDIT (GET) ====================
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _staffService.GetStaffForEditAsync(id);
            if (model == null) return NotFound();

            await SetViewBagFromMasterData(User);
            return View(model);
        }

        // ==================== EDIT (POST) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
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
            if (IsEmptyInvalidBinding(modelState, nameof(StaffEditVM.BaseSalary)))
            {
                model.BaseSalary = 0;
                modelState.Remove(nameof(StaffEditVM.BaseSalary));
            }

            if (IsEmptyInvalidBinding(modelState, nameof(StaffEditVM.Allowance)))
            {
                model.Allowance = 0;
                modelState.Remove(nameof(StaffEditVM.Allowance));
            }

            if (IsEmptyInvalidBinding(modelState, nameof(StaffEditVM.ProbationRate)))
            {
                model.ProbationRate = 0;
                modelState.Remove(nameof(StaffEditVM.ProbationRate));
            }

            if (IsEmptyInvalidBinding(modelState, nameof(StaffEditVM.OvertimeRate)))
            {
                model.OvertimeRate = 0;
                modelState.Remove(nameof(StaffEditVM.OvertimeRate));
            }

            if (IsEmptyInvalidBinding(modelState, nameof(StaffEditVM.PrimaryBankIndex)))
            {
                model.PrimaryBankIndex = 0;
                modelState.Remove(nameof(StaffEditVM.PrimaryBankIndex));
            }

            // Nested Dependents[i].DateOfBirth empty
            foreach (var key in modelState.Keys
                         .Where(k => k.Contains("DateOfBirth", StringComparison.OrdinalIgnoreCase))
                         .ToList())
            {
                if (IsEmptyInvalidBinding(modelState, key))
                    modelState.Remove(key);
            }

            // Empty optional BHYT fails MinimumLength(10); treat blank as null.
            if (string.IsNullOrWhiteSpace(model.HealthInsuranceNumber))
            {
                model.HealthInsuranceNumber = null;
                modelState.Remove(nameof(StaffEditVM.HealthInsuranceNumber));
            }

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
                    nameof(StaffEditVM.BaseSalary) or nameof(StaffEditVM.Allowance)
                        or nameof(StaffEditVM.ProbationRate) or nameof(StaffEditVM.OvertimeRate)
                        => "Giá trị số không hợp lệ.",
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
        public async Task<IActionResult> ManualResetPassword([FromBody] ManualResetRequest req)
        {
            var account = await _dbContext.Accounts.FindAsync(req.AccountId);
            if (account == null)
                return Json(new { success = false, message = "Không tìm thấy hồ sơ cá nhân của nhân sự này!" });

            // Using BCrypt to match standard hashing
            account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
            
            _dbContext.Update(account);
            await _dbContext.SaveChangesAsync();

            return Json(new { success = true, message = "Đã cập nhật mật khẩu mới và mã hóa thành công!" });
        }

        // ==================== DYNAMIC DROPDOWN API ====================
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetScopeReferences(int scopeTypeId, int? parentId = null)
        {
            try
            {
                var data = await _staffService.GetScopeReferencesAsync(scopeTypeId, parentId);
                return Json(data);
            }
            catch
            {
                return Json(new object[] { });
            }
        }
    }

    public class ManualResetRequest {
        public int AccountId { get; set; }
        public string NewPassword { get; set; }
    }
}

