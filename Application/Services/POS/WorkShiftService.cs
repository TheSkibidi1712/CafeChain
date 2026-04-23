using CafeChain.Application.Interfaces.Attendance;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.POS
{
    public class WorkShiftService : IWorkShiftService
    {
        private readonly AppDbContext _context;
        private readonly IHrAttendanceService _hrAttendanceService;

        public WorkShiftService(AppDbContext context, IHrAttendanceService hrAttendanceService)
        {
            _context = context;
            _hrAttendanceService = hrAttendanceService;
        }

        public async Task<ServiceResult> OpenShiftAsync(int userId, int storeId, decimal startingCash)
        {
            try
            {
                // 1. HR INTERLOCK CHECK (The Gatekeeper)
                bool hasValidCheckIn = await _hrAttendanceService.VerifyRecentCheckInAsync(userId, storeId);
                
                if (!hasValidCheckIn)
                {
                    return ServiceResult.Failure("Từ chối truy cập: Vui lòng sử dụng điện thoại cá nhân kết nối Wifi quán và quét khuôn mặt để Chấm công trước khi Nhận ca POS!");
                }

                // 2. STATE CHECK: Prevent multiple open shifts for the same user/store
                var activeShift = await _context.WorkShifts
                    .FirstOrDefaultAsync(ws => ws.UserId == userId && ws.StoreId == storeId && ws.Status == "Open");

                if (activeShift != null)
                {
                    return ServiceResult.Failure("Bạn đang có một ca làm việc chưa được đóng. Vui lòng đóng ca trước khi nhận ca mới.");
                }

                // 3. EXECUTION: Open Financial Shift
                var newShift = new WorkShift
                {
                    UserId = userId,
                    StoreId = storeId,
                    StartTime = DateTime.Now,
                    StartingCash = startingCash,
                    ExpectedEndingCash = startingCash,
                    Status = "Open"
                };

                _context.WorkShifts.Add(newShift);
                await _context.SaveChangesAsync();

                return ServiceResult.Success("Mở ca thành công! Chào mừng bạn.");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure("Lỗi hệ thống khi mở ca: " + ex.Message);
            }
        }
    }
}
