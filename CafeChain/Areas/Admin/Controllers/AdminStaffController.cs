using CafeChain.Application.Interfaces.Admin.Staffs;
using CafeChain.ViewModels.Admin.Staffs;
using CafeChain.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

            if (!ModelState.IsValid)
            {
                var errors = string.Join("<br/>", ModelState.Values
                                        .SelectMany(v => v.Errors)
                                        .Select(e => e.ErrorMessage));
                TempData["Error"] = "Cập nhật thất bại:<br/>" + errors;
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

