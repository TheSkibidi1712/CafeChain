using CafeChain.Application.DTOs.Admin.Toppings;
using CafeChain.Application.Interfaces.Admin.Toppings;
using CafeChain.ViewModels.Admin.Toppings;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminToppingController : Controller
    {
        private readonly IAdminToppingService _service;

        public AdminToppingController(IAdminToppingService service)
        {
            _service = service;
        }

        // =============================
        // List Toppings
        // =============================
        public async Task<IActionResult> Index()
        {
            var toppings = await _service.GetAllAsync(); // ✅ FIX QUAN TRỌNG

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

        // =============================
        // Create Topping (Modal)
        // =============================
        [HttpPost]
        public async Task<IActionResult> Create(AdminToppingVM vm)
        {
            // ❗ Validate ảnh khi create
            if (vm.ImageFile == null)
            {
                ModelState.AddModelError("ImageFile", "Vui lòng chọn ảnh");
            }

            if (!ModelState.IsValid)
            {
                var list = await _service.GetAllAsync();
                return View("Index", list.Select(x => new AdminToppingVM
                {
                    ToppingId = x.ToppingId,
                    Name = x.Name,
                    Price = x.Price,
                    ImageUrl = x.ImageUrl,
                    Active = x.Active
                }));
            }

            try
            {
                string imagePath = null;

                if (vm.ImageFile != null)
                {
                    var fileName = Guid.NewGuid() + Path.GetExtension(vm.ImageFile.FileName);
                    var path = Path.Combine("wwwroot/Images/ToppingImages", fileName);

                    using var stream = new FileStream(path, FileMode.Create);
                    await vm.ImageFile.CopyToAsync(stream);

                    imagePath = "/Images/ToppingImages/" + fileName;
                }

                await _service.CreateAsync(new ToppingDto
                {
                    Name = vm.Name,
                    Price = vm.Price,
                    ImageUrl = imagePath
                });

                TempData["success"] = "Thêm topping thành công";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);

                var list = await _service.GetAllAsync();
                return View("Index", list.Select(x => new AdminToppingVM
                {
                    ToppingId = x.ToppingId,
                    Name = x.Name,
                    Price = x.Price,
                    ImageUrl = x.ImageUrl,
                    Active = x.Active
                }));
            }
        }

        // =============================
        // Edit Topping (Modal)
        // =============================
        [HttpPost]
        public async Task<IActionResult> Edit(AdminToppingVM vm)
        {
            try
            {
                string imagePath = vm.ImageUrl;

                if (vm.ImageFile != null)
                {
                    var fileName = Guid.NewGuid() + Path.GetExtension(vm.ImageFile.FileName);
                    var path = Path.Combine("wwwroot/Images/ToppingImages", fileName);

                    using var stream = new FileStream(path, FileMode.Create);
                    await vm.ImageFile.CopyToAsync(stream);

                    imagePath = "/Images/ToppingImages/" + fileName;
                }

                await _service.UpdateAsync(new ToppingDto
                {
                    ToppingId = vm.ToppingId,
                    Name = vm.Name,
                    Price = vm.Price,
                    ImageUrl = imagePath
                });

                TempData["success"] = "Cập nhật thành công";
            }
            catch (Exception ex)
            {
                TempData["error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // =============================
        // Toggle Status
        // =============================
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            await _service.ToggleStatusAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
