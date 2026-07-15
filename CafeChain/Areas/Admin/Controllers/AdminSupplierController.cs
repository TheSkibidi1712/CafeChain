using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.Interfaces.Admin.Suppliers;
using CafeChain.Application.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SupplierReadRoles)]
    public class AdminSupplierController : Controller
    {
        private const string SupplierReadRoles =
            RoleConstants.BusinessOwner + "," + RoleConstants.AccountantWarehouse + "," +
            RoleConstants.AreaManager + "," + RoleConstants.StoreManager;
        private const string SupplierMutationRoles =
            RoleConstants.BusinessOwner + "," + RoleConstants.AccountantWarehouse;
        private readonly IAdminSupplierService _service;

        public AdminSupplierController(IAdminSupplierService service)
        {
            _service = service;
        }

        // ===== INDEX =====
        public async Task<IActionResult> Index(string? search, bool? status)
        {
            var data = await _service.GetAllAsync(search, status);
            return View(data);
        }

        // ===== GET DETAIL (for detail modal / page) =====
        [HttpGet]
        public async Task<IActionResult> GetById(int id)
        {
            var data = await _service.GetByIdAsync(id);
            if (data == null)
                return Json(new { success = false, message = "Không tìm thấy nhà cung cấp" });

            return Json(new { success = true, data });
        }

        // ===== GET NEXT CODE (auto-generate NCC code) =====
        [HttpGet]
        public async Task<IActionResult> GetNextCode()
        {
            var code = await _service.GenerateNextCodeAsync();
            return Json(new { success = true, code });
        }

        // ===== CREATE =====
        [HttpPost]
        [Authorize(Roles = SupplierMutationRoles)]
        public async Task<IActionResult> Create([FromBody] AdminSupplierCreateDTO dto)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

            try
            {
                var id = await _service.CreateAsync(dto);
                return Json(new { success = true, message = "Thêm nhà cung cấp thành công", data = id });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===== UPDATE =====
        [HttpPost]
        [Authorize(Roles = SupplierMutationRoles)]
        public async Task<IActionResult> Update([FromBody] AdminSupplierUpdateDTO dto)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dữ liệu không hợp lệ" });

            try
            {
                await _service.UpdateAsync(dto);
                return Json(new { success = true, message = "Cập nhật thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===== TOGGLE STATUS =====
        [HttpPost]
        [Authorize(Roles = SupplierMutationRoles)]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                await _service.ToggleStatusAsync(id);
                return Json(new { success = true, message = "Đã cập nhật trạng thái" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===================== PHONES =====================

        [HttpPost]
        [Authorize(Roles = SupplierMutationRoles)]
        public async Task<IActionResult> AddPhone([FromBody] AdminSupplierPhoneCreateDTO dto)
        {
            try
            {
                await _service.AddPhoneAsync(dto);
                return Json(new { success = true, message = "Thêm số điện thoại thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = SupplierMutationRoles)]
        public async Task<IActionResult> DeletePhone(int supplierPhoneId)
        {
            try
            {
                await _service.DeletePhoneAsync(supplierPhoneId);
                return Json(new { success = true, message = "Đã xoá số điện thoại" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===================== CONTACTS =====================

        [HttpPost]
        [Authorize(Roles = SupplierMutationRoles)]
        public async Task<IActionResult> AddContact([FromBody] AdminSupplierContactCreateDTO dto)
        {
            try
            {
                await _service.AddContactAsync(dto);
                return Json(new { success = true, message = "Thêm liên hệ thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = SupplierMutationRoles)]
        public async Task<IActionResult> UpdateContact([FromBody] AdminSupplierContactUpdateDTO dto)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dữ liệu liên hệ không hợp lệ" });

            try
            {
                await _service.UpdateContactAsync(dto);
                return Json(new { success = true, message = "Đã cập nhật người liên hệ" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = SupplierMutationRoles)]
        public async Task<IActionResult> DeleteContact(int supplierContactId)
        {
            try
            {
                await _service.DeleteContactAsync(supplierContactId);
                return Json(new { success = true, message = "Đã xoá liên hệ" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = SupplierMutationRoles)]
        public async Task<IActionResult> SetPrimaryContact(int supplierContactId)
        {
            try
            {
                await _service.SetPrimaryContactAsync(supplierContactId);
                return Json(new { success = true, message = "Đã cập nhật người liên hệ chính" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===================== INGREDIENT SUPPLIER OFFERS (#111) =====================

        [HttpGet]
        public async Task<IActionResult> GetIngredientOffers(int supplierId)
        {
            var data = await _service.GetIngredientOffersAsync(supplierId);
            return Json(new { success = true, data });
        }

        [HttpGet]
        public async Task<IActionResult> GetIngredientOffer(int id)
        {
            var data = await _service.GetIngredientOfferByIdAsync(id);
            if (data == null)
                return Json(new { success = false, message = "Không tìm thấy bảng giá gói mua" });
            return Json(new { success = true, data });
        }

        [HttpPost]
        [Authorize(Roles = SupplierMutationRoles)]
        public async Task<IActionResult> CreateIngredientOffer([FromBody] AdminIngredientSupplierSaveDTO dto)
        {
            try
            {
                var id = await _service.CreateIngredientOfferAsync(dto);
                return Json(new { success = true, message = "Thêm gói mua nguyên liệu thành công", data = id });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = SupplierMutationRoles)]
        public async Task<IActionResult> UpdateIngredientOffer([FromBody] AdminIngredientSupplierSaveDTO dto)
        {
            try
            {
                await _service.UpdateIngredientOfferAsync(dto);
                return Json(new { success = true, message = "Cập nhật gói mua nguyên liệu thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = SupplierMutationRoles)]
        public async Task<IActionResult> ToggleIngredientOffer([FromBody] AdminIngredientSupplierToggleDTO dto)
        {
            try
            {
                await _service.ToggleIngredientOfferActiveAsync(dto.IngredientSupplierId, dto.Active);
                return Json(new { success = true, message = "Đã cập nhật trạng thái gói mua" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetIngredientOptions()
        {
            var data = await _service.GetIngredientDropdownAsync();
            return Json(new { success = true, data });
        }

        [HttpGet]
        public async Task<IActionResult> GetContentUnitOptions()
        {
            var data = await _service.GetContentUnitDropdownAsync();
            return Json(new { success = true, data });
        }

    }
}
