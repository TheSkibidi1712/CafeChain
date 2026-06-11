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

        // Đặt thành false để bật kiểm tra chấm công thực tế (Production Mode)
        private const bool BYPASS_MODE = true;

        public HrAttendanceService(IAttendanceRepository repository)
        {
            _repository = repository;
        }

        public async Task<bool> VerifyRecentCheckInAsync(int userId, int storeId)
        {
            // [BYPASS] Tạm thời bỏ kiểm tra để test — đổi BYPASS_MODE = false khi deploy
            if (BYPASS_MODE) return true;

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
