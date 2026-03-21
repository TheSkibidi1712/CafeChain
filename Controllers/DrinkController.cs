using CafeChain.Application.Interfaces;
using CafeChain.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Controllers
{
    public class DrinkController : Controller
    {
        private readonly IDrinkService _drinkService;

        // Tiêm IDrinkService vào Controller
        public DrinkController(IDrinkService drinkService)
        {
            _drinkService = drinkService;
        }

        public async Task<IActionResult> Menu(int? categoryId, decimal minPrice = 0, decimal maxPrice = 150000, string sortBy = "popular", int page = 1)
        {
            int pageSize = 8;

            // Gọi Service để lấy cục dữ liệu
            var data = await _drinkService.GetMenuDataAsync(categoryId, minPrice, maxPrice, sortBy, page, pageSize);

            // Ráp vào ViewModel
            var viewModel = new MenuViewModel
            {
                Categories = data.Categories,
                Drinks = data.Drinks,
                SelectedCategoryId = categoryId,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                SortBy = sortBy,
                CurrentPage = page,
                TotalPages = data.TotalPages
            };

            return View(viewModel);
        }
        public async Task<IActionResult> Detail(int id)
        {
            var viewModel = await _drinkService.GetDrinkDetailAsync(id);
            if (viewModel == null) return NotFound();

            // Server đã nạp đủ Image, Size, ToppingMuaThem, ToppingMacDinh vào cục ViewModel này rồi
            return View(viewModel);
        }
    }
}