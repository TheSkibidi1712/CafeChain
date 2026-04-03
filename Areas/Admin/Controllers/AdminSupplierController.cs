using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.Interfaces.Admin.Suppliers;
using CafeChain.ViewModels.Admin.Suppliers;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminSupplierController : Controller
    {
        private readonly IAdminSupplierService _supplierService;

        public AdminSupplierController(IAdminSupplierService supplierService)
        {
            _supplierService = supplierService;
        }

        public async Task<IActionResult> Index()
        {
            var suppliers = await _supplierService.GetAllSuppliersAsync();
            return View(suppliers);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var viewModel = new AdminSupplierCreateViewModel
            {
                SupplierCreateDTO = new AdminSupplierCreateDTO()
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminSupplierCreateViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _supplierService.CreateSupplierAsync(viewModel.SupplierCreateDTO);
                    TempData["SuccessMessage"] = "Thêm nhà cung cấp thành công!";
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
            var updateDTO = await _supplierService.GetSupplierForUpdateAsync(id);
            if (updateDTO == null) return NotFound();

            var viewModel = new AdminSupplierEditViewModel
            {
                SupplierUpdateDTO = updateDTO
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminSupplierEditViewModel viewModel)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _supplierService.UpdateSupplierAsync(viewModel.SupplierUpdateDTO);
                    TempData["SuccessMessage"] = "Cập nhật nhà cung cấp thành công!";
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
                await _supplierService.ToggleSupplierStatusAsync(id);
                return Json(new { success = true, message = "Đã cập nhật trạng thái nhà cung cấp." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> AdjustDebt(int id, decimal amount)
        {
            if (amount == 0)
                return Json(new { success = false, message = "Số tiền phải lớn hơn 0." });

            try
            {
                await _supplierService.AdjustDebtAsync(id, amount);
                return Json(new { success = true, message = "Đã cập nhật công nợ thành công." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }
    }
}
