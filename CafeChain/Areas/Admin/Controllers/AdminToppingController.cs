using CafeChain.Application.DTOs.Admin.DrinkToppings;
using CafeChain.Application.DTOs.Admin.Toppings;
using CafeChain.Application.Interfaces.Admin.DrinkToppings;
using CafeChain.Application.Interfaces.Admin.Toppings;
using CafeChain.ViewModels.Admin.DrinkToppings;
using CafeChain.ViewModels.Admin.Toppings;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    public class AdminToppingController : AdminBaseController
    {
        private readonly IAdminToppingService _toppingService;
        private readonly IAdminDrinkToppingService _drinkToppingService;

        public AdminToppingController(
            IAdminToppingService toppingService,
            IAdminDrinkToppingService drinkToppingService)
        {
            _toppingService = toppingService;
            _drinkToppingService = drinkToppingService;
        }

        // =====================================================
        // INDEX
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var toppings =
                await _toppingService.GetAllAsync();

            var vm = toppings.Select(x => new AdminToppingVM
            {
                ToppingId = x.ToppingId,
                Name = x.Name,
                Price = x.Price,
                ImageUrl = x.ImageUrl,
                Active = x.Active
            });

            return View(vm);
        }

        // =====================================================
        // CREATE TOPPING
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            AdminToppingVM vm)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Json(new
                    {
                        success = false,
                        message = GetModelStateError()
                    });
                }

                await _toppingService.CreateAsync(
                    MapToDto(vm));

                return Json(new
                {
                    success = true,
                    message = "Thêm topping thành công"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // =====================================================
        // UPDATE TOPPING
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            AdminToppingVM vm)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Json(new
                    {
                        success = false,
                        message = GetModelStateError()
                    });
                }

                await _toppingService.UpdateAsync(
                    MapToDto(vm));

                return Json(new
                {
                    success = true,
                    message = "Cập nhật topping thành công"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // =====================================================
        // TOGGLE TOPPING STATUS
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(
            int id)
        {
            try
            {
                await _toppingService
                    .ToggleStatusAsync(id);

                return Json(new
                {
                    success = true,
                    message = "Cập nhật trạng thái thành công"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // =====================================================
        // GET DRINKS OF TOPPING
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> GetDrinks(int toppingId)
        {
            try
            {
                var result =
                    await _drinkToppingService
                        .GetDrinksForToppingAsync(toppingId);

                return Json(new
                {
                    success = true,
                    data = result
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // =====================================================
        // ASSIGN TOPPING TO DRINK
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign([FromBody] AssignDrinkToppingVM vm)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = GetModelStateError()
                });
            }

            try
            {
                var dto = new DrinkToppingDto
                {
                    DrinkId = vm.DrinkId,
                    ToppingId = vm.ToppingId
                };

                await _drinkToppingService.AssignAsync(dto);

                return Json(new
                {
                    success = true,
                    message = "Gán topping thành công"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // =====================================================
        // TOGGLE DRINK TOPPING
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "Id không hợp lệ"
                    });
                }

                await _drinkToppingService.ToggleAsync(id);

                return Json(new
                {
                    success = true,
                    message = "Cập nhật trạng thái thành công"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // =====================================================
        // PRIVATE HELPERS
        // =====================================================

        private static ToppingDto MapToDto(AdminToppingVM vm)
        {
            return new ToppingDto
            {
                ToppingId = vm.ToppingId,
                Name = vm.Name,
                Price = vm.Price,
                ImageFile = vm.ImageFile
            };
        }

        private string GetModelStateError()
        {
            return ModelState.Values
                .SelectMany(x => x.Errors)
                .Select(x => x.ErrorMessage)
                .FirstOrDefault()
                ?? "Dữ liệu không hợp lệ";
        }
    }
}