using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using CafeChain.Application.Interfaces.Admin;
using CafeChain.Application.DTOs.Admin.Inventory;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminInventoryController : Controller
    {
        private readonly IAdminIngredientService _ingredientService;

        public AdminInventoryController(IAdminIngredientService ingredientService)
        {
            _ingredientService = ingredientService;
        }

        public async Task<IActionResult> Index(int pageIndex = 1, string searchTerm = "", string type = "", string status = "")
        {
            int pageSize = 5; // As per the UI image (Hiển thị 1-5 trong số 142)
            var vm = await _ingredientService.GetInventoryDashboardAsync(pageIndex, pageSize, searchTerm, type, status);
            return View(vm);
        }

        public async Task<IActionResult> Export(string searchTerm = "", string type = "", string status = "")
        {
            var csvBytes = await _ingredientService.ExportInventoryCsvAsync(searchTerm, type, status);
            var fileName = $"TonKho_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";
            return File(csvBytes, "text/csv", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> Import()
        {
            var ingredients = await _ingredientService.GetIngredientsForDropdownAsync();
            ViewBag.Ingredients = new SelectList(ingredients, "IngredientId", "Name");
            return View(new AdminCreateStockImportDto());
        }

        [HttpPost]
        public async Task<IActionResult> Import(AdminCreateStockImportDto dto)
        {
            if (ModelState.IsValid)
            {
                // Mock StoreId = 1, StaffId = 1
                var success = await _ingredientService.CreateStockImportAsync(dto, 1, 1);
                if (success)
                {
                    return RedirectToAction(nameof(Index));
                }
                ModelState.AddModelError("", "Đã có lỗi xảy ra khi lưu phiếu nhập kho. Vui lòng kiểm tra lại dữ liệu.");
            }
            
            var ingredients = await _ingredientService.GetIngredientsForDropdownAsync();
            ViewBag.Ingredients = new SelectList(ingredients, "IngredientId", "Name");
            return View(dto);
        }
    }
}
