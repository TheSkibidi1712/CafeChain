using CafeChain.Application.DTOs.Admin.DrinkSizes;
using CafeChain.Application.DTOs.Admin.Sizes;
using CafeChain.Application.Interfaces.Admin.DrinkSizes;
using CafeChain.Application.Interfaces.Admin.Sizes;
using CafeChain.ViewModels.Admin.DrinkSizes;
using CafeChain.ViewModels.Admin.Sizes;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    public class AdminSizeController : AdminBaseController
    {
        private readonly IAdminSizeService _adminsizeService;
        private readonly IAdminDrinkSizeService _drinkSizeService;

        public AdminSizeController(IAdminSizeService adminsizeService, IAdminDrinkSizeService adminDrinkSizeService)
        {
            _adminsizeService = adminsizeService;
            _drinkSizeService = adminDrinkSizeService;
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
        public async Task<IActionResult> Create([FromBody] AdminSizeVM vm)
        {
            if (vm == null || string.IsNullOrWhiteSpace(vm.Name))
                return BadRequest("Dữ liệu không hợp lệ");

            var result = await _adminsizeService.CreateSizeAsync(new SizeDto
            {
                Name = vm.Name,
                Description = vm.Description
            });

            if (!result.Success)
                return BadRequest(result.Error);

            return Ok();
        }

        // =============================
        // Edit Size
        // =============================
        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] AdminSizeVM vm)
        {
            if (vm == null || vm.SizeId <= 0)
                return BadRequest("Không tìm thấy size");

            var result = await _adminsizeService.UpdateSizeAsync(new SizeDto
            {
                SizeId = vm.SizeId,
                Name = vm.Name,
                Description = vm.Description
            });

            if (!result.Success)
                return BadRequest(result.Error);

            return Ok();
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

        // =============================
        // List Drinks
        // =============================
        [HttpGet]
        public async Task<IActionResult> GetDrinks(int sizeId)
        {
            var data = await _drinkSizeService.GetDrinksForSizeAsync(sizeId);
            return Json(data);
        }

        // =============================
        // Assign Drink to Size
        // =============================
        [HttpPost]
        public async Task<IActionResult> AssignDrink([FromBody] AssignDrinkSizeVM vm)
        {
            if (!ModelState.IsValid)
                return BadRequest("Invalid data");

            try
            {
                await _drinkSizeService.AssignDrinkAsync(new DrinkSizeDto
                {
                    DrinkId = vm.DrinkId,
                    SizeId = vm.SizeId,
                    Price = vm.Price
                });

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // =============================
        // Toggle Drink-Size Assignment
        // =============================
        [HttpPost]
        public async Task<IActionResult> ToggleDrinkSize(int id)
        {
            try
            {
                await _drinkSizeService.ToggleDrinkSizeAsync(id);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // =============================
        // Update Drink-Size Price
        // =============================
        [HttpPost]
        public async Task<IActionResult> UpdatePrice(int drinkSizeId, decimal price)
        {
            if (price <= 0)
                return BadRequest("Giá không hợp lệ");

            await _drinkSizeService.UpdatePriceAsync(new DrinkSizeDto
            {
                DrinkSizeId = drinkSizeId,
                Price = price
            });

            return Ok();
        }
    }
}

