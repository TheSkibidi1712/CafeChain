using CafeChain.Application.DTOs.Admin.Drinks;
using CafeChain.Application.Interfaces.Admin.Drinks;
using CafeChain.ViewModels.Admin.Drinks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CafeChain.Areas.Admin.Controllers
{
    public class AdminDrinkController : AdminBaseController
    {
        private readonly IAdminDrinkService _drinkService;

        public AdminDrinkController(IAdminDrinkService drinkService)
        {
            _drinkService = drinkService;
        }

        [HttpGet]
        public async Task<IActionResult> IndexPartial()
        {
            var drinks = await _drinkService.GetAllDrinksAsync();
            return PartialView("_DrinkTablePartial", drinks);
        }

        public async Task<IActionResult> Index()
        {
            var drinks = await _drinkService.GetAllDrinksAsync();
            return View(drinks);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var categories = await _drinkService.GetDrinkCategoriesAsync();
            var productTypes = await _drinkService.GetProductTypesAsync();

            var viewModel = new AdminDrinkCreateViewModel
            {
                DrinkCreateDTO = new AdminDrinkCreateDTO(),
                Categories = categories.Select(c => new SelectListItem
                {
                    Value = c.CategoryId.ToString(),
                    Text = c.Name
                }),
                ProductTypes = productTypes.Select(pt => new SelectListItem
                {
                    Value = pt.ProductTypeId.ToString(),
                    Text = pt.Name
                })
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminDrinkCreateViewModel viewModel)
        {
            ModelState.Remove("DrinkCreateDTO.ImageFiles");
            ModelState.Remove(nameof(viewModel.Categories));
            ModelState.Remove(nameof(viewModel.ProductTypes));

            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Dữ liệu không hợp lệ"
                });
            }

            try
            {
                await _drinkService.CreateDrinkAsync(viewModel.DrinkCreateDTO);

                return Json(new
                {
                    success = true,
                    message = "Thêm đồ uống thành công!",
                    redirectUrl = Url.Action("Index")

                });
            }
            catch (ArgumentException ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception)
            {
                return Json(new
                {
                    success = false,
                    message = "Đã có lỗi xảy ra"
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var updateDTO = await _drinkService.GetDrinkForUpdateAsync(id);
            if (updateDTO == null) return NotFound();

            var categories = await _drinkService.GetDrinkCategoriesAsync();
            var productTypes = await _drinkService.GetProductTypesAsync();

            var viewModel = new AdminDrinkEditViewModel
            {
                DrinkUpdateDTO = updateDTO,
                Categories = categories.Select(c => new SelectListItem { Value = c.CategoryId.ToString(), Text = c.Name }),
                ProductTypes = productTypes.Select(pt => new SelectListItem { Value = pt.ProductTypeId.ToString(), Text = pt.Name })
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminDrinkEditViewModel viewModel)
        {
            ModelState.Remove(nameof(viewModel.Categories));
            ModelState.Remove(nameof(viewModel.ProductTypes));

            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Dữ liệu không hợp lệ"
                });
            }

            try
            {
                await _drinkService.UpdateDrinkAsync(viewModel.DrinkUpdateDTO);

                return Json(new
                {
                    success = true,
                    message = "Cập nhật đồ uống thành công!",
                    redirectUrl = Url.Action("Index")

                });
            }
            catch (ArgumentException ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
            catch (Exception)
            {
                return Json(new
                {
                    success = false,
                    message = "Đã có lỗi xảy ra"
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                await _drinkService.ToggleDrinkStatusAsync(id);
                return Json(new { success = true, message = "Đã cập nhật trạng thái đồ uống thành công." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi cập nhật trạng thái: " + ex.Message });
            }
        }

        // --- Image Management (AJAX) ---
        [HttpGet]
        public async Task<IActionResult> GetImages(int drinkId)
        {
            var images = await _drinkService.GetDrinkImagesAsync(drinkId);
            return Json(new { success = true, data = images });
        }

        [HttpPost]
        public async Task<IActionResult> UploadImage(int drinkId, IFormFile imageFile, bool isDefault)
        {
            try
            {
                if (imageFile == null || imageFile.Length == 0)
                    return Json(new { success = false, message = "Vui lòng chọn ảnh." });

                await _drinkService.AddDrinkImageAsync(drinkId, imageFile, isDefault);
                return Json(new { success = true, message = "Thêm ảnh thành công." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SetDefaultImage(int drinkId, int drinkImageId)
        {
            try
            {
                await _drinkService.SetDefaultDrinkImageAsync(drinkId, drinkImageId);
                return Json(new { success = true, message = "Đã cập nhật ảnh mặc định." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteImage(int drinkImageId)
        {
            try
            {
                await _drinkService.DeleteDrinkImageAsync(drinkImageId);

                return Json(new
                {
                    success = true,
                    message = "Đã xóa ảnh thành công."
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
        public async Task<IActionResult> UpdateImage(int drinkImageId, IFormFile newImageFile)
        {
            try
            {
                if (newImageFile == null || newImageFile.Length == 0)
                    return Json(new { success = false, message = "Vui lòng chọn ảnh mới." });

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                var ext = Path.GetExtension(newImageFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(ext))
                    return Json(new { success = false, message = "Chỉ chấp nhận file JPG hoặc PNG." });

                await _drinkService.UpdateDrinkImageAsync(drinkImageId, newImageFile);
                return Json(new { success = true, message = "Cập nhật ảnh thành công." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }
    }
}

