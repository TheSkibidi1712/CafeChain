using CafeChain.Application.Interfaces.Admin.Staff;
using CafeChain.ViewModels.Admin.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin System, Store Manager")]
    public class AdminStaffController : Controller
    {
        private readonly IAdminStaffService _staffService;

        public AdminStaffController(IAdminStaffService staffService)
        {
            _staffService = staffService;
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

        private async Task SetViewBagFromMasterData(int? storeId)
        {
            var masterData = await _staffService.GetMasterDataForFormAsync(storeId);
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
            await SetViewBagFromMasterData(storeId);
            return View(model);
        }

        // ==================== CREATE (POST) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StaffCreateVM model)
        {
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

            await SetViewBagFromMasterData(GetCurrentManagerStoreId());
            return View(model);
        }

        // ==================== EDIT (POST) ====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StaffEditVM model)
        {
            if (id != model.StaffId) return BadRequest();

            try
            {
                var result = await _staffService.UpdateStaffAsync(model, User, model.AvatarFile);

                if (!result.IsSuccess)
                {
                    TempData["Error"] = result.Message;
                    await SetViewBagFromMasterData(GetCurrentManagerStoreId());
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
    }
}
