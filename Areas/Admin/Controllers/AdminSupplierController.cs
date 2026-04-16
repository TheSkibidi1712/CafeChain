using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.Interfaces.Admin.Suppliers;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminSupplierController : Controller
    {
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

        // ===== CREATE =====
        [HttpPost]
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

        // ===================== BANK ACCOUNTS =====================

        [HttpPost]
        public async Task<IActionResult> AddBankAccount([FromBody] AdminSupplierBankAccountCreateDTO dto)
        {
            try
            {
                await _service.AddBankAccountAsync(dto);
                return Json(new { success = true, message = "Thêm tài khoản ngân hàng thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteBankAccount(int supplierBankAccountId)
        {
            try
            {
                await _service.DeleteBankAccountAsync(supplierBankAccountId);
                return Json(new { success = true, message = "Đã xoá tài khoản ngân hàng" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ===================== CONTACTS =====================

        [HttpPost]
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
    }
}
