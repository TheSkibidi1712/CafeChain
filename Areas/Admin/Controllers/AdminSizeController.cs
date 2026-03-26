using CafeChain.Application.DTOs.Admin.Sizes;
using CafeChain.Application.Interfaces.Admin.Sizes;
using CafeChain.ViewModels.Admin.Sizes;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminSizeController : Controller
    {
        private readonly IAdminSizeService _adminsizeService;
        public AdminSizeController(IAdminSizeService adminsizeService)
        {
            _adminsizeService = adminsizeService;
        }

        // =============================
        // List Sizes
        // =============================
        public async Task<IActionResult> Index()
        {
            var sizes = await _adminsizeService.GetActiveSizesAsync();

            var vm = sizes.Select(s => new AdminSizeVM
            {
                SizeId = s.SizeId,
                Name = s.Name,
                Description = s.Description,
                Active = s.Active
            });

            return View(vm);
        }

        // =============================
        // Create Size
        // =============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminSizeVM vm)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Dữ liệu không hợp lệ";
                return RedirectToAction(nameof(Index));
            }

            var result = await _adminsizeService.CreateSizeAsync(new SizeDto
            {
                Name = vm.Name,
                Description = vm.Description
            });

            if (!result.Success)
            {
                TempData["Error"] = result.Error;
            }

            return RedirectToAction(nameof(Index));
        }

        // =============================
        // Edit Size
        // =============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminSizeVM vm)
        {
            var result = await _adminsizeService.UpdateSizeAsync(new SizeDto
            {
                SizeId = vm.SizeId,
                Name = vm.Name,
                Description = vm.Description
            });

            if (!result.Success)
            {
                TempData["Error"] = result.Error;
            }

            return RedirectToAction(nameof(Index));
        }

        // =============================
        // Toggle Size Status
        // =============================
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            await _adminsizeService.ToggleStatusAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
