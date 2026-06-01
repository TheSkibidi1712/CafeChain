using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Attendance;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
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

        public async Task<WorkShift?> GetActiveShiftAsync(int userId, int storeId)
        {
            return await _context.WorkShifts
                .FirstOrDefaultAsync(ws => ws.UserId == userId && ws.StoreId == storeId && ws.Status == "Open");
        }

        public async Task<ServiceResult> CloseShiftAsync(int userId, int storeId, CloseShiftRequestDto request)
        {
            try
            {
                // 1. Find active shift
                var activeShift = await _context.WorkShifts
                    .FirstOrDefaultAsync(ws => ws.UserId == userId && ws.StoreId == storeId && ws.Status == "Open");

                if (activeShift == null)
                {
                    return ServiceResult.Failure("Không tìm thấy ca két tiền đang mở.");
                }

                // 2. Calculate Expected Ending Cash
                // ExpectedEndingCash = StartingCash + Sum of cash payments in this shift's orders
                var totalCashSales = await _context.Orders
                    .Where(o => o.WorkShiftId == activeShift.ShiftId)
                    .Join(_context.Payments,
                        o => o.OrderId,
                        p => p.OrderId,
                        (o, p) => new { Order = o, Payment = p })
                    .Where(op => op.Payment.PaymentMethodId == 1) // 1 = Cash payment
                    .SumAsync(op => (decimal?)op.Payment.Amount) ?? 0m;

                // [FIX Lỗi 2] Đã xóa khối fallback if (totalCashSales == 0)
                // Nếu không có giao dịch tiền mặt, totalCashSales = 0 là chính xác
                // ExpectedEndingCash = StartingCash + 0 = StartingCash

                var expectedEndingCash = activeShift.StartingCash + totalCashSales;

                // 3. Record Discrepancy
                var discrepancy = request.ActualEndingCash - expectedEndingCash;

                if (discrepancy != 0 && string.IsNullOrWhiteSpace(request.DiscrepancyReason))
                {
                    return ServiceResult.Failure($"Phát hiện chênh lệch {discrepancy:N0}đ. Vui lòng nhập lý do chênh lệch.");
                }

                // 4. Close Shift
                activeShift.ExpectedEndingCash = expectedEndingCash;
                activeShift.ActualEndingCash = request.ActualEndingCash;
                activeShift.DiscrepancyReason = request.DiscrepancyReason;
                activeShift.EndTime = DateTime.Now;
                activeShift.Status = "Closed";

                await _context.SaveChangesAsync();

                return ServiceResult.Success($"Đóng ca thành công! Doanh thu ca: {totalCashSales:N0}đ. Chênh lệch: {discrepancy:N0}đ.");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure("Lỗi hệ thống khi đóng ca: " + ex.Message);
            }
        }
    }
}
