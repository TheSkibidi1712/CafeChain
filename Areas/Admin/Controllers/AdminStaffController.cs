using CafeChain.Application.Interfaces.Admin.Staffs;
using CafeChain.ViewModels.Admin.Staffs;
using CafeChain.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "RequireAdminPanelAccess")]
    public class AdminStaffController : Controller
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
                await SetViewBagFromMasterData(User);
                return PartialView("_CreateStaffModal", model);
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
                await SetViewBagFromMasterData(User);
                return View(model);
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
        
        // ==================== DYNAMIC DROPDOWN API ====================
        [HttpGet]
        public async Task<IActionResult> GetScopeReferences(int scopeTypeId)
        {
            try
            {
                if (scopeTypeId == 1) // HQ
                {
                    return Json(new[] { new { id = 1, name = "Trụ sở chính" } });
                }
                else if (scopeTypeId == 2) // Province
                {
                    var provinces = await _dbContext.Provinces.Select(p => new { id = p.ProvinceId, name = p.Name }).ToListAsync();
                    return Json(provinces);
                }
                else if (scopeTypeId == 3) // Ward
                {
                    var wards = await _dbContext.Wards.Select(w => new { id = w.WardId, name = w.Name }).ToListAsync();
                    return Json(wards);
                }
                else if (scopeTypeId == 4) // Store
                {
                    var stores = await _dbContext.Stores.Where(s => s.Active).Select(s => new { id = s.StoreId, name = s.Name }).ToListAsync();
                    return Json(stores);
                }

                return Json(new object[] { });
            }
            catch
            {
                return Json(new object[] { });
            }
        }
    }
}
