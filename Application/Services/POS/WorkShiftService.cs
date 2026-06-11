using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Attendance;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Stores;
using System;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.POS
{
    public class WorkShiftService : IWorkShiftService
    {
        private readonly IWorkShiftRepository _shiftRepo;
        private readonly IHrAttendanceService _hrAttendanceService;
        private readonly Infrastrusture.Interfaces.Admin.POS.IPOSOrderRepository _posRepo;

        public WorkShiftService(
            IWorkShiftRepository shiftRepo,
            IHrAttendanceService hrAttendanceService,
            Infrastrusture.Interfaces.Admin.POS.IPOSOrderRepository posRepo)
        {
            _shiftRepo = shiftRepo;
            _hrAttendanceService = hrAttendanceService;
            _posRepo = posRepo;
        }

        public async Task<ServiceResult> OpenShiftAsync(int userId, int storeId, decimal startingCash, string? posTerminalId = null)
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
                var activeShift = await _shiftRepo.GetActiveShiftAsync(userId, storeId);

                if (activeShift != null)
                {
                    return ServiceResult.Failure("Bạn đang có một ca làm việc chưa được đóng. Vui lòng đóng ca trước khi nhận ca mới.");
                }

                // 3. LATE OPENING GUARD: Kiểm tra mở ca trễ > 30 phút
                var staffShiftToday = await _shiftRepo.GetTodayStaffShiftAsync(userId);

                if (staffShiftToday != null && staffShiftToday.Shift != null)
                {
                    var today = DateTime.Today;
                    var shiftStartTime = today.Add(staffShiftToday.Shift.StartTime);
                    var minutesLate = (DateTime.Now - shiftStartTime).TotalMinutes;

                    if (minutesLate > 30)
                    {
                        // Kiểm tra xem đã có audit log bypass trong 5 phút qua chưa
                        var pendingBypass = await _posRepo.GetPendingAuditLogAsync(userId, "OPEN_SHIFT_LATE", 5);
                        if (pendingBypass == null)
                        {
                            return ServiceResult.Failure(
                                $"LATE_OPENING_REQUIRES_BYPASS|Ca của bạn bắt đầu lúc {staffShiftToday.Shift.StartTime:hh\\:mm}. Bạn đã trễ hơn 30 phút. Yêu cầu Trưởng ca xác thực mã PIN để mở ca trễ.");
                        }
                    }
                }

                // 4. EXECUTION: Open Financial Shift
                var newShift = new WorkShift
                {
                    UserId = userId,
                    StoreId = storeId,
                    StartTime = DateTime.Now,
                    StartingCash = startingCash,
                    ExpectedEndingCash = startingCash,
                    Status = "Open",
                    PosTerminalId = posTerminalId
                };

                await _shiftRepo.CreateShiftAsync(newShift);

                // 5. Nếu có pending bypass mở ca trễ, liên kết ShiftId vào audit log
                if (staffShiftToday != null && staffShiftToday.Shift != null)
                {
                    var today = DateTime.Today;
                    var shiftStartTime = today.Add(staffShiftToday.Shift.StartTime);
                    if ((DateTime.Now - shiftStartTime).TotalMinutes > 30)
                    {
                        var pendingBypass = await _posRepo.GetPendingAuditLogAsync(userId, "OPEN_SHIFT_LATE", 5);
                        if (pendingBypass != null)
                        {
                            await _posRepo.UpdateAuditLogOrderIdAsync(pendingBypass.Id, newShift.ShiftId);
                        }
                    }
                }

                return ServiceResult.Success("Mở ca thành công! Chào mừng bạn.");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure("Lỗi hệ thống khi mở ca: " + ex.Message);
            }
        }

        public async Task<WorkShift?> GetActiveShiftAsync(int userId, int storeId)
        {
            return await _shiftRepo.GetActiveShiftAsync(userId, storeId);
        }

        public async Task<ServiceResult> CloseShiftAsync(int userId, int storeId, CloseShiftRequestDto request)
        {
            try
            {
                // 1. Find active shift
                var activeShift = await _shiftRepo.GetActiveShiftAsync(userId, storeId);

                if (activeShift == null)
                {
                    return ServiceResult.Failure("Không tìm thấy ca két tiền đang mở.");
                }

                // 2. Calculate Expected Ending Cash via Repository
                var totalCashSales = await _shiftRepo.GetTotalCashSalesAsync(activeShift.ShiftId);

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

                await _shiftRepo.UpdateShiftAsync(activeShift);

                return ServiceResult.Success($"Đóng ca thành công! Doanh thu ca: {totalCashSales:N0}đ. Chênh lệch: {discrepancy:N0}đ.");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure("Lỗi hệ thống khi đóng ca: " + ex.Message);
            }
        }
    }
}
