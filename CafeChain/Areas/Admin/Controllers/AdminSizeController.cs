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

        public AdminSizeController(
            IAdminSizeService adminsizeService,
            IAdminDrinkSizeService adminDrinkSizeService)
        {
            _adminsizeService = adminsizeService;
            _drinkSizeService = adminDrinkSizeService;
        }

        public async Task<IActionResult> Index()
        {
            var sizes = await _adminsizeService.GetActiveSizesAsync();

            var vm = sizes.Select(s => new AdminSizeVM
            {
                SizeId = s.SizeId,
                SizeCode = s.SizeCode,
                Name = s.Name,
                Description = s.Description,
                SizeType = s.SizeType,
                Active = s.Active
            });

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AdminSizeVM vm)
        {
            if (vm == null)
            {
                return Json(new
                {
                    success = false,
                    message = "Dữ liệu không hợp lệ"
                });
            }

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
                var result = await _adminsizeService.CreateSizeAsync(new SizeDto
                {
                    SizeCode = vm.SizeCode,
                    Name = vm.Name,
                    Description = vm.Description,
                    SizeType = vm.SizeType
                });

                if (!result.Success)
                {
                    return Json(new
                    {
                        success = false,
                        message = result.Error
                    });
                }

                return Json(new
                {
                    success = true,
                    message = "Tạo size thành công"
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

        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] AdminSizeVM vm)
        {
            if (vm == null || vm.SizeId <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "Không tìm thấy size"
                });
            }

            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = GetModelStateError()
                });
            }

            var result = await _adminsizeService.UpdateSizeAsync(new SizeDto
            {
                SizeId = vm.SizeId,
                SizeCode = vm.SizeCode,
                Name = vm.Name,
                Description = vm.Description,
                SizeType = vm.SizeType
            });

            if (!result.Success)
            {
                return Json(new
                {
                    success = false,
                    message = result.Error
                });
            }

            return Json(new
            {
                success = true,
                message = "Cập nhật size thành công"
            });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                await _adminsizeService.ToggleStatusAsync(id);

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

        [HttpGet]
        public async Task<IActionResult> GetDrinks(int sizeId)
        {
            var data = await _drinkSizeService.GetDrinksForSizeAsync(sizeId);
            return Json(data);
        }

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
