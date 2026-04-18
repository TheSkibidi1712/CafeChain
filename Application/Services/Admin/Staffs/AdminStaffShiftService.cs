using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CafeChain.Application.Interfaces.Admin.Staffs;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Staffs;

namespace CafeChain.Application.Services.Admin.Staffs
{
    public class AdminStaffShiftService : IAdminStaffShiftService
    {
        private readonly AppDbContext _context;

        public AdminStaffShiftService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Dictionary<Staff, List<StaffShift>>> GetShiftMatrixAsync(int storeId, DateTime startDate, DateTime endDate)
        {
            // 🔥 BƯỚC 3: DATABASE LINQ DATE-FILTER (Chống Data Over-fetching)
            // Ép hệ thống dùng SQL where thẳng trong DB thay vì Load tất cả về RAM
            var shiftsInRange = await _context.StaffShifts
                .Include(s => s.Staff)
                .Include(s => s.Shift)
                .Where(s => s.Staff.StoreId == storeId && 
                            s.WorkDate.Date >= startDate.Date && 
                            s.WorkDate.Date <= endDate.Date &&
                            s.Staff.Active)
                .ToListAsync();

            // Load nhân viên thuộc Store, có chức vụ cấp cửa hàng (IsStoreLevel = true)
            // Phải lấy những nv Đang làm HOẶC (Đã nghỉ nhưng CÓ CA trong tuần này)
            var staffs = await _context.Staffs
                .Include(s => s.Account)
                    .ThenInclude(a => a.AccountRoles)
                        .ThenInclude(ar => ar.Role)
                .Where(s => s.StoreId == storeId)
                .Where(s => s.Account.AccountRoles.Any(ar => ar.Role.IsStoreLevel))
                .Where(s => s.Active || s.StaffShifts.Any(ss => ss.WorkDate >= startDate.Date && ss.WorkDate <= endDate.Date))
                .ToListAsync();

            var matrix = new Dictionary<Staff, List<StaffShift>>();
            
            foreach (var staff in staffs)
            {
                var staffShifts = shiftsInRange.Where(s => s.StaffId == staff.StaffId).OrderBy(s => s.WorkDate).ToList();
                matrix.Add(staff, staffShifts);
            }

            return matrix;
        }

        // ====================================================================
        // HÀM TIỆN ÍCH: Chuyển TimeSpan thành DateTime tuyệt đối để so sánh
        // Giải quyết triệt để Ca Qua Đêm (ví dụ: 22:00 → 06:00)
        // ====================================================================
        private (DateTime start, DateTime end) ResolveAbsoluteTime(DateTime workDate, TimeSpan start, TimeSpan end, bool isOvernight)
        {
            var dtStart = workDate.Date + start;
            var dtEnd = workDate.Date + end;

            // Nếu là ca qua đêm HOẶC giờ kết thúc <= giờ bắt đầu → cộng thêm 1 ngày cho End
            if (isOvernight || end <= start)
            {
                dtEnd = dtEnd.AddDays(1);
            }

            return (dtStart, dtEnd);
        }

        // ====================================================================
        // HÀM TIỆN ÍCH: Kiểm tra trùng lặp thời gian giữa 2 khoảng DateTime
        // Toán tử giao điểm: (StartA < EndB) && (EndA > StartB)
        // ====================================================================
        private bool IsOverlapping(DateTime startA, DateTime endA, DateTime startB, DateTime endB)
        {
            return startA < endB && endA > startB;
        }

        public async Task<ServiceResult> AssignShiftAsync(int staffId, int shiftId, DateTime date, TimeSpan? customStart = null, TimeSpan? customEnd = null)
        {
            // 1. Áp dụng Rule Ân hạn (Grace Period: 3 ngày)
            var gracePeriodDate = DateTime.Today.AddDays(-3);
            if (date.Date < gracePeriodDate)
            {
                return ServiceResult.Failure("Lỗi: Thời điểm vượt quá giới hạn 3 ngày cho phép xếp ca trong quá khứ.");
            }

            var staff = await _context.Staffs.FindAsync(staffId);
            if (staff == null || !staff.Active || staff.EmployeeStatus == 3) // Trạng thái 3 = Nghỉ việc
            {
                return ServiceResult.Failure("Lỗi: Không thể xếp ca vì nhân viên không tồn tại hoặc đã nghỉ việc.");
            }

            var shift = await _context.Shifts.FindAsync(shiftId);
            if (shift == null || !shift.Active)
            {
                return ServiceResult.Failure("Lỗi: Ca làm việc không hợp lệ.");
            }

            // Thời gian ca thực tế cần lưu
            TimeSpan actualStart = customStart ?? shift.StartTime;
            TimeSpan actualEnd = customEnd ?? shift.EndTime;

            // Validate logic thời gian (chỉ kiểm tra nếu KHÔNG phải ca qua đêm)
            bool isOvernightShift = shift.IsOvernight || actualEnd <= actualStart;
            if (actualStart >= actualEnd && !isOvernightShift)
            {
                return ServiceResult.Failure("Lỗi: Thời gian bắt đầu phải trước thời gian kết thúc.");
            }

            // Chuyển đổi sang DateTime tuyệt đối để so sánh chính xác (xử lý ca qua đêm)
            var (newStart, newEnd) = ResolveAbsoluteTime(date, actualStart, actualEnd, isOvernightShift);

            // 2. Chống Trùng lặp (Overlapping Validation) — Tạo mới nên không cần loại trừ bản ghi nào
            var existingShifts = await _context.StaffShifts
                .Include(ss => ss.Shift)
                .Where(ss => ss.StaffId == staffId && ss.WorkDate.Date == date.Date)
                .ToListAsync();

            foreach (var existing in existingShifts)
            {
                TimeSpan eStart = existing.CustomStartTime ?? existing.Shift.StartTime;
                TimeSpan eEnd = existing.CustomEndTime ?? existing.Shift.EndTime;
                bool eIsOvernight = existing.Shift.IsOvernight || eEnd <= eStart;

                var (exStart, exEnd) = ResolveAbsoluteTime(existing.WorkDate, eStart, eEnd, eIsOvernight);

                if (IsOverlapping(newStart, newEnd, exStart, exEnd))
                {
                    return ServiceResult.Failure("Lỗi: Thời gian ca làm việc bị trùng lặp!");
                }
            }

            var staffShift = new StaffShift
            {
                StaffId = staffId,
                ShiftId = shiftId,
                WorkDate = date.Date,
                CustomStartTime = customStart,
                CustomEndTime = customEnd,
                StatusId = 1 // 1: Được Xếp (Planned)
            };

            await _context.StaffShifts.AddAsync(staffShift);
            await _context.SaveChangesAsync();

            return ServiceResult.Success("Đã gán ca thành công!");
        }

        // ====================================================================
        // CẬP NHẬT CA CỦA NHÂN VIÊN (Edit StaffShift)
        // Bản vá: Self-Overlap + Wage Theft Guard + Night Shift Math
        // ====================================================================
        public async Task<ServiceResult> UpdateStaffShiftAsync(int staffShiftId, int shiftId, TimeSpan? customStart = null, TimeSpan? customEnd = null)
        {
            var staffShift = await _context.StaffShifts
                .Include(ss => ss.Shift)
                .FirstOrDefaultAsync(ss => ss.StaffShiftId == staffShiftId);

            if (staffShift == null)
            {
                return ServiceResult.Failure("Lỗi: Bản ghi ca làm việc không tồn tại.");
            }

            // ===== GUARD CLAUSE: Chống gian lận tiền lương (Wage Theft) =====
            // Nếu nhân viên đã chấm công vào ca này → KHÔNG được phép sửa giờ
            if (staffShift.ActualCheckIn != null)
            {
                return ServiceResult.Failure("Lỗi: Không thể sửa lịch của ca làm việc đã có dữ liệu chấm công.");
            }

            // Áp dụng Rule Ân hạn (Grace Period: 3 ngày)
            var gracePeriodDate = DateTime.Today.AddDays(-3);
            if (staffShift.WorkDate.Date < gracePeriodDate)
            {
                return ServiceResult.Failure("Lỗi: Thời điểm vượt quá giới hạn 3 ngày cho phép chỉnh sửa ca trong quá khứ.");
            }

            var shift = await _context.Shifts.FindAsync(shiftId);
            if (shift == null || !shift.Active)
            {
                return ServiceResult.Failure("Lỗi: Ca làm việc không hợp lệ.");
            }

            TimeSpan actualStart = customStart ?? shift.StartTime;
            TimeSpan actualEnd = customEnd ?? shift.EndTime;

            bool isOvernightShift = shift.IsOvernight || actualEnd <= actualStart;
            if (actualStart >= actualEnd && !isOvernightShift)
            {
                return ServiceResult.Failure("Lỗi: Thời gian bắt đầu phải trước thời gian kết thúc.");
            }

            var (newStart, newEnd) = ResolveAbsoluteTime(staffShift.WorkDate, actualStart, actualEnd, isOvernightShift);

            // ===== Chống trùng lặp — LOẠI TRỪ bản ghi đang sửa (Self-Overlap Fix) =====
            var existingShifts = await _context.StaffShifts
                .Include(ss => ss.Shift)
                .Where(ss => ss.StaffId == staffShift.StaffId
                          && ss.WorkDate.Date == staffShift.WorkDate.Date
                          && ss.StaffShiftId != staffShiftId) // <-- Loại trừ chính nó
                .ToListAsync();

            foreach (var existing in existingShifts)
            {
                TimeSpan eStart = existing.CustomStartTime ?? existing.Shift.StartTime;
                TimeSpan eEnd = existing.CustomEndTime ?? existing.Shift.EndTime;
                bool eIsOvernight = existing.Shift.IsOvernight || eEnd <= eStart;

                var (exStart, exEnd) = ResolveAbsoluteTime(existing.WorkDate, eStart, eEnd, eIsOvernight);

                if (IsOverlapping(newStart, newEnd, exStart, exEnd))
                {
                    return ServiceResult.Failure("Lỗi: Thời gian ca làm việc bị trùng lặp!");
                }
            }

            // Cập nhật dữ liệu
            staffShift.ShiftId = shiftId;
            staffShift.CustomStartTime = customStart;
            staffShift.CustomEndTime = customEnd;

            _context.Update(staffShift);
            await _context.SaveChangesAsync();

            return ServiceResult.Success("Cập nhật ca làm việc của nhân viên thành công!");
        }

        public async Task<List<object>> GetShiftsForStoreAsync(int storeId)
        {
            var shifts = await _context.Shifts
                .Where(s => s.StoreId == storeId && s.Active)
                .OrderBy(s => s.StartTime)
                .Select(s => new
                {
                    s.ShiftId,
                    s.Name,
                    startTime = s.StartTime.ToString(@"hh\:mm"),
                    endTime = s.EndTime.ToString(@"hh\:mm"),
                    s.IsOvernight,
                    s.Notes
                })
                .ToListAsync();
            return shifts.Cast<object>().ToList();
        }

        public async Task<ServiceResult> UpdateShiftAsync(int shiftId, TimeSpan startTime, TimeSpan endTime, string? notes)
        {
            var shift = await _context.Shifts.FindAsync(shiftId);
            if (shift == null) return ServiceResult.Failure("Ca làm việc không tồn tại.");

            if (startTime >= endTime && !shift.IsOvernight)
                return ServiceResult.Failure("Giờ bắt đầu phải trước giờ kết thúc (trừ ca qua đêm).");

            shift.StartTime = startTime;
            shift.EndTime = endTime;
            shift.Notes = notes;

            _context.Update(shift);
            await _context.SaveChangesAsync();

            return ServiceResult.Success("Cập nhật ca làm việc thành công!");
        }
    }
}
