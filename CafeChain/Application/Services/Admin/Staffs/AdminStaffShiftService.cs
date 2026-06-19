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
            // Không còn chặn xếp ca trong quá khứ theo yêu cầu mới.

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

            // ====================================================================
            // RECONCILIATION: Tự động liên kết các bản ghi Ca Tự Do (Orphan)
            // Khi Manager tạo ca mới cho ngày đã có chấm công Ad-hoc,
            // hệ thống sẽ "nhận nuôi" bản ghi mồ côi đó vào ca chính thức.
            // ====================================================================
            var orphanRecords = await _context.StaffShifts
                .Where(ss => ss.StaffId == staffId
                           && ss.WorkDate.Date == date.Date
                           && ss.IsAdHoc == true
                           && ss.ShiftId == null
                           && ss.StaffShiftId != staffShift.StaffShiftId)
                .ToListAsync();

            foreach (var orphan in orphanRecords)
            {
                orphan.ShiftId = shiftId;
                orphan.IsAdHoc = false;

                // Tính lại PayrollHours nếu đã có dữ liệu CheckIn/CheckOut
                if (orphan.ActualCheckIn.HasValue && orphan.ActualCheckOut.HasValue)
                {
                    var checkIn = orphan.ActualCheckIn.Value;
                    var checkOut = orphan.ActualCheckOut.Value;

                    // Xử lý ca qua đêm
                    if (checkOut < checkIn)
                        checkOut = checkOut.AddDays(1);

                    var totalMinutes = (checkOut - checkIn).TotalMinutes;
                    var roundedMinutes = Math.Round(totalMinutes / 15.0, MidpointRounding.AwayFromZero) * 15;
                    orphan.PayrollHours = Math.Round((decimal)(roundedMinutes / 60.0), 2);
                }

                _context.Update(orphan);
            }

            if (orphanRecords.Any())
            {
                await _context.SaveChangesAsync();
            }

            var reconcileMsg = orphanRecords.Any()
                ? $" Đã tự động liên kết {orphanRecords.Count} bản ghi chấm công phát sinh."
                : "";

            return ServiceResult.Success($"Đã gán ca thành công!{reconcileMsg}");
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

            // Đã loại bỏ khoá chống gian lận tiền lương theo yêu cầu.
            // Cho phép HR chỉnh sửa lại (CustomStart / CustomEnd) để tính toán PayrollHours phù hợp.

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

            staffShift.ShiftId = shiftId;
            staffShift.CustomStartTime = customStart;
            staffShift.CustomEndTime = customEnd;

            if (staffShift.ActualCheckIn.HasValue && staffShift.ActualCheckOut.HasValue)
            {
                staffShift.PayrollHours = (decimal)Math.Round((staffShift.ActualCheckOut.Value - staffShift.ActualCheckIn.Value).TotalHours, 2);
            }

            _context.Update(staffShift);
            await _context.SaveChangesAsync();

            return ServiceResult.Success("Cập nhật ca làm việc của nhân viên thành công!");
        }

        public async Task<List<object>> GetShiftsForStoreAsync(int storeId)
        {
            var shifts = await _context.Shifts
                .Where(s => s.StoreId == storeId && s.Active)
                .OrderBy(s => s.StartTime)
                .ToListAsync();

            // BẮT BUỘC (ENFORCE): Nếu chưa cóủ 4 ca chuẩn, tự động tạo và lưu trữ.
            string[] requiredShifts = { "Ca 1", "Ca 2", "Ca 3", "Ca 4" };
            bool hasChanges = false;

            if (!shifts.Any(s => s.Name == "Ca 1"))
            {
                var ca1 = new Shift { Name = "Ca 1", StartTime = new TimeSpan(6, 0, 0), EndTime = new TimeSpan(12, 0, 0), IsOvernight = false, Active = true, StoreId = storeId, Duration = TimeSpan.FromHours(6), Notes = "06:00 - 12:00" };
                _context.Shifts.Add(ca1); shifts.Add(ca1); hasChanges = true;
            }
            if (!shifts.Any(s => s.Name == "Ca 2"))
            {
                var ca2 = new Shift { Name = "Ca 2", StartTime = new TimeSpan(12, 0, 0), EndTime = new TimeSpan(18, 0, 0), IsOvernight = false, Active = true, StoreId = storeId, Duration = TimeSpan.FromHours(6), Notes = "12:00 - 18:00" };
                _context.Shifts.Add(ca2); shifts.Add(ca2); hasChanges = true;
            }
            if (!shifts.Any(s => s.Name == "Ca 3"))
            {
                var ca3 = new Shift { Name = "Ca 3", StartTime = new TimeSpan(18, 0, 0), EndTime = new TimeSpan(23, 0, 0), IsOvernight = false, Active = true, StoreId = storeId, Duration = TimeSpan.FromHours(5), Notes = "18:00 - 23:00" };
                _context.Shifts.Add(ca3); shifts.Add(ca3); hasChanges = true;
            }
            if (!shifts.Any(s => s.Name == "Ca 4"))
            {
                var ca4 = new Shift { Name = "Ca 4", StartTime = new TimeSpan(22, 0, 0), EndTime = new TimeSpan(6, 0, 0), IsOvernight = true, Active = true, StoreId = storeId, Duration = TimeSpan.FromHours(8), Notes = "22:00 - 06:00 (Hôm sau)" };
                _context.Shifts.Add(ca4); shifts.Add(ca4); hasChanges = true;
            }

            if (hasChanges)
            {
                await _context.SaveChangesAsync();
                // Sắp xếp lại
                shifts = shifts.OrderBy(s => s.StartTime).ToList();
            }

            return shifts.Select(s => new
                {
                    s.ShiftId,
                    s.Name,
                    startTime = s.StartTime.ToString(@"hh\:mm"),
                    endTime = s.EndTime.ToString(@"hh\:mm"),
                    s.IsOvernight,
                    s.Notes
                })
                .Cast<object>().ToList();
        }

        public async Task<ServiceResult> UpdateShiftAsync(int shiftId, TimeSpan startTime, TimeSpan endTime, string? notes)
        {
            var shift = await _context.Shifts.FindAsync(shiftId);
            if (shift == null) return ServiceResult.Failure("Ca làm việc không tồn tại.");

            bool isOvernight = endTime <= startTime;
            if (startTime >= endTime && !isOvernight)
                return ServiceResult.Failure("Giờ bắt đầu phải trước giờ kết thúc (trừ ca qua đêm).");

            shift.StartTime = startTime;
            shift.EndTime = endTime;
            shift.IsOvernight = isOvernight;
            shift.Notes = notes;

            if (isOvernight)
                shift.Duration = (TimeSpan.FromHours(24) - startTime) + endTime;
            else
                shift.Duration = endTime - startTime;

            _context.Update(shift);
            await _context.SaveChangesAsync();

            return ServiceResult.Success("Cập nhật ca làm việc thành công!");
        }
    }
}
