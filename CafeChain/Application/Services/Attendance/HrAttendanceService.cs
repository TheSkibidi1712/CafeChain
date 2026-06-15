using CafeChain.Application.Interfaces.Attendance;
using CafeChain.Infrastructure.Interfaces.Attendance;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.Attendance
{
    public class HrAttendanceService : IHrAttendanceService
    {
        private readonly IAttendanceRepository _repository;

        // Production Mode: Nhân viên PHẢI chấm công (FaceID + Wi-Fi) trước khi mở ca POS
        private const bool BYPASS_MODE = false;

        public HrAttendanceService(IAttendanceRepository repository)
        {
            _repository = repository;
        }

        public async Task<bool> VerifyRecentCheckInAsync(int userId, int storeId)
        {
            // [CHẶNG 2] Đã gỡ bypass — kiểm tra chấm công thực tế
            // Nhân viên phải có ca đã check-in nhưng chưa check-out (hôm nay hoặc ca qua đêm hôm qua)

            // [FIX Lỗi 1] Đổi từ kiểm tra AttendanceLog 30 phút sang kiểm tra StaffShift đang mở
            // Nhân viên phải có ca chấm công đã check-in nhưng chưa check-out (hôm nay hoặc hôm qua cho ca qua đêm)
            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);

            var shifts = await _repository.GetStaffShiftsWithLockAsync(userId, today, yesterday);
            return shifts.Any(ss =>
                ss.ActualCheckIn.HasValue
                && !ss.ActualCheckOut.HasValue);
        }
    }
}
