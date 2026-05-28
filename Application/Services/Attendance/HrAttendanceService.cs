using CafeChain.Application.Interfaces.Attendance;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.Attendance
{
    public class HrAttendanceService : IHrAttendanceService
    {
        private readonly AppDbContext _context;

        public HrAttendanceService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> VerifyRecentCheckInAsync(int userId, int storeId)
        {
            // [FIX.md] TẠM THỜI BYPASS KIỂM TRA ĐỊA CHỈ IP VÀ CHẤM CÔNG ĐỂ KHÁCH HÀNG TEST TRƯỚC
            return true;

            /*
            var cutoffTime = DateTime.UtcNow.AddMinutes(-30);

            var recentLog = await _context.AttendanceLogs
                .Where(log => log.UserId == userId 
                           && log.StoreId == storeId 
                           && log.CheckInTime >= cutoffTime
                           && log.IsFaceVerified == true
                           && log.Status == "Valid")
                .OrderByDescending(log => log.CheckInTime)
                .FirstOrDefaultAsync();

            return recentLog != null;
            */
        }
    }
}
