using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CafeChain.Application.Interfaces.Admin.Staffs;
using Microsoft.AspNetCore.Authorization;

namespace CafeChain.Areas.Admin.Controllers
{
    // [Authorize] -> Bật lên nếu dự án đang dùng Identity Authentication
    public class AdminStaffShiftController : AdminBaseController
    {
        private readonly IAdminStaffShiftService _shiftService;
        private readonly CafeChain.Data.AppDbContext _context;

        public AdminStaffShiftController(IAdminStaffShiftService shiftService, CafeChain.Data.AppDbContext context)
        {
            _shiftService = shiftService;
            _context = context;
        }

        // Helper trích xuất StoreId từ Context
        // Nếu là Manager, lấy StoreId của họ. Nếu là SuperAdmin, dùng targetStoreId.
        private int ResolveStoreId(int? targetStoreId)
        {
            var storeIdClaim = User.FindFirst("StoreId")?.Value;
            if (int.TryParse(storeIdClaim, out int sid) && sid > 0)
            {
                // Là Quản lý cửa hàng -> BẮT BUỘC dùng Store của chính họ (Bảo mật RBAC)
                return sid;
            }

            // Nếu không có StoreId (Super Admin) thì dùng targetStoreId truyền lên
            if (targetStoreId.HasValue && targetStoreId.Value > 0)
                return targetStoreId.Value;

            // Lần truy cập đầu tiên của SuperAdmin có thể không có targetStoreId -> mặc định trả cửa hàng đầu tiên
            return _context.Stores.OrderBy(s => s.StoreId).Select(s => s.StoreId).FirstOrDefault();
        }

        // GET: /Admin/AdminStaffShift/Index
        public async Task<IActionResult> Index(DateTime? startDate, int? targetStoreId)
        {
            var date = startDate ?? DateTime.Today;
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            var startOfWeek = date.AddDays(-1 * diff).Date;
            var endOfWeek = startOfWeek.AddDays(6).Date;

            // Xem Role để load danh sách cửa hàng
            var storeIdClaim = User.FindFirst("StoreId")?.Value;
            bool isSuperAdmin = string.IsNullOrEmpty(storeIdClaim);
            ViewBag.IsSuperAdmin = isSuperAdmin;
            if (isSuperAdmin)
            {
                ViewBag.Stores = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(_context.Stores).ToList();
            }

            int effectiveStoreId = ResolveStoreId(targetStoreId);
            ViewBag.ActiveStoreId = effectiveStoreId;

            // Lấy tên cửa hàng để hiển thị ngữ cảnh trên giao diện
            ViewBag.ActiveStoreName = _context.Stores
                .Where(s => s.StoreId == effectiveStoreId)
                .Select(s => s.Name)
                .FirstOrDefault() ?? "N/A";

            // Nếu không có cửa hàng nào trên hệ thống
            if (effectiveStoreId == 0)
                return View(new System.Collections.Generic.Dictionary<CafeChain.Models.Staffs.Staff, System.Collections.Generic.List<CafeChain.Models.Staffs.StaffShift>>());

            var matrixResult = await _shiftService.GetShiftMatrixAsync(effectiveStoreId, startOfWeek, endOfWeek);

            ViewBag.StartDate = startOfWeek;
            
            return View(matrixResult);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignShift(int staffId, int shiftId, DateTime date, int? targetStoreId, TimeSpan? customStart, TimeSpan? customEnd)
        {
            try
            {
                var managerStoreId = ResolveStoreId(targetStoreId);
                var result = await _shiftService.AssignShiftAsync(staffId, shiftId, date, customStart, customEnd);
                if (result.IsSuccess)
                    return Ok(new { success = true, message = result.Message });
                
                return BadRequest(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        // GET AJAX: Lấy danh sách ca thuộc Store hiện tại (cho dropdown modal)
        [HttpGet]
        public async Task<IActionResult> GetShifts(int? targetStoreId)
        {
            try {
                var storeId = ResolveStoreId(targetStoreId);
                var shifts = await _shiftService.GetShiftsForStoreAsync(storeId);
                return Json(shifts);
            } catch {
                return Json(new List<object>()); // Trả về rỗng nếu lỗi
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateShift(int shiftId, TimeSpan startTime, TimeSpan endTime, string? notes)
        {
            try
            {
                var result = await _shiftService.UpdateShiftAsync(shiftId, startTime, endTime, notes);
                if (result.IsSuccess)
                    return Ok(new { success = true, message = result.Message });
                return BadRequest(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // POST AJAX: Cập nhật ca của nhân viên (Edit StaffShift)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStaffShift(int staffShiftId, int shiftId, TimeSpan? customStart, TimeSpan? customEnd)
        {
            try
            {
                var result = await _shiftService.UpdateStaffShiftAsync(staffShiftId, shiftId, customStart, customEnd);
                if (result.IsSuccess)
                    return Ok(new { success = true, message = result.Message });
                return BadRequest(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi: " + ex.Message });
            }
        }
    }
}

