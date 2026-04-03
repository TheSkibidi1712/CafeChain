using CafeChain.Application.DTOs.Admin.Ingredients;
using CafeChain.Application.Interfaces.Admin.Ingredients;
using CafeChain.ViewModels.Admin.Ingredients;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminIngredientController : Controller
    {
        private readonly IAdminIngredientService _ingredientService;

        public AdminIngredientController(IAdminIngredientService ingredientService)
        {
            _ingredientService = ingredientService;
        }

        public async Task<IActionResult> Index()
        {
            var ingredients = await _ingredientService.GetAllIngredientsAsync();
            return View(ingredients);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var viewModel = new AdminIngredientCreateViewModel
            {
                IngredientCreateDTO = new AdminIngredientCreateDTO()
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminIngredientCreateViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _ingredientService.CreateIngredientAsync(viewModel.IngredientCreateDTO);
                    TempData["SuccessMessage"] = "Thêm nguyên liệu thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (ArgumentException ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            }
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var updateDTO = await _ingredientService.GetIngredientForUpdateAsync(id);
            if (updateDTO == null) return NotFound();

            var viewModel = new AdminIngredientEditViewModel
            {
                IngredientUpdateDTO = updateDTO
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminIngredientEditViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _ingredientService.UpdateIngredientAsync(viewModel.IngredientUpdateDTO);
                    TempData["SuccessMessage"] = "Cập nhật nguyên liệu thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (ArgumentException ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
                catch (Exception)
                {
                    ModelState.AddModelError("", "Đã có lỗi xảy ra. Vui lòng thử lại.");
                }
            }
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                await _ingredientService.ToggleIngredientStatusAsync(id);
                return Json(new { success = true, message = "Đã cập nhật trạng thái nguyên liệu." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }
    }
}
