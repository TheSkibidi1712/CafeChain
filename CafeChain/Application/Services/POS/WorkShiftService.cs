using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Attendance;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Stores;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.POS
{
    public class WorkShiftService : IWorkShiftService
    {
        private readonly IWorkShiftRepository _shiftRepo;
        private readonly IHrAttendanceService _hrAttendanceService;
        private readonly IPOSOrderRepository _posRepo;
        private readonly ISupervisorAuthService _supervisorAuthService;
        private readonly ILogger<WorkShiftService> _logger;

        public WorkShiftService(
            IWorkShiftRepository shiftRepo,
            IHrAttendanceService hrAttendanceService,
            IPOSOrderRepository posRepo,
            ISupervisorAuthService supervisorAuthService,
            ILogger<WorkShiftService> logger)
        {
            _shiftRepo = shiftRepo;
            _hrAttendanceService = hrAttendanceService;
            _posRepo = posRepo;
            _supervisorAuthService = supervisorAuthService;
            _logger = logger;
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
                        var pendingBypass = await _posRepo.GetPendingAuditLogAsync(userId, "OPEN_SHIFT_LATE", 5);
                        if (pendingBypass == null)
                        {
                            return ServiceResult.Failure(
                                $"LATE_OPENING_REQUIRES_BYPASS|Ca của bạn bắt đầu lúc {staffShiftToday.Shift.StartTime:hh\\:mm}. Bạn đã trễ hơn 30 phút. Yêu cầu Trưởng ca xác thực mã PIN để mở ca trễ.");
                        }
                    }
                }

                // 4. Ensure POS terminal exists before WorkShift FK insert
                var normalizedTerminalId = string.IsNullOrWhiteSpace(posTerminalId)
                    ? null
                    : posTerminalId.Trim();

                if (normalizedTerminalId != null)
                {
                    var terminalName = $"POS-Store{storeId}-{DateTime.Now:MMdd-HHmm}";
                    await _shiftRepo.EnsurePosTerminalAsync(normalizedTerminalId, storeId, terminalName);
                }

                // 5. EXECUTION: Open Financial Shift
                var newShift = new WorkShift
                {
                    UserId = userId,
                    StoreId = storeId,
                    StartTime = DateTime.Now,
                    StartingCash = startingCash,
                    ExpectedEndingCash = startingCash,
                    Status = "Open",
                    PosTerminalId = normalizedTerminalId
                };

                await _shiftRepo.CreateShiftAsync(newShift);

                // 6. Nếu có pending bypass mở ca trễ, liên kết ShiftId vào audit log
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

        public async Task<WorkShift?> GetShiftByIdAsync(int shiftId, int userId, int storeId)
        {
            return await _shiftRepo.GetShiftByIdAsync(shiftId, userId, storeId);
        }

        // ============================================================
        // CLOSE SHIFT + RECONCILIATION: Đối soát két tiền cuối ca
        // ============================================================
        /// <summary>
        /// Đóng ca POS + đối soát két tiền:
        ///   1. Tính ExpectedEndingCash = StartingCash + Σ Cash Sales trong ca
        ///   2. CashDiscrepancy = ActualEndingCash - ExpectedEndingCash
        ///   3. Nếu lệch != 0 → ghi Warning log cho Web Admin đối soát
        ///   4. Persist toàn bộ lên WorkShift
        /// </summary>
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

                // 2. Backend-known PayOS/VietQR pending orders block normal close.
                if (await _shiftRepo.HasOpenPosPaymentAsync(activeShift.ShiftId, storeId))
                {
                    return ServiceResult.Failure(
                        "Không thể đóng ca thường. Đang có giao dịch thanh toán chưa hoàn tất. " +
                        "Vui lòng hoàn tất hoặc hủy giao dịch trước khi đóng ca.");
                }

                // 3. Calculate Expected Ending Cash via Repository
                var totalCashSales = await _shiftRepo.GetTotalCashSalesAsync(activeShift.ShiftId);

                // ExpectedEndingCash = StartingCash + tổng doanh thu tiền mặt trong ca
                var expectedEndingCash = activeShift.StartingCash + totalCashSales;

                // 4. Calculate Discrepancy
                var discrepancy = request.ActualEndingCash - expectedEndingCash;

                if (discrepancy != 0 && string.IsNullOrWhiteSpace(request.DiscrepancyReason))
                {
                    return ServiceResult.Failure($"Phát hiện chênh lệch {discrepancy:N0}đ. Vui lòng nhập lý do chênh lệch.");
                }

                // 5. Close Shift — persist reconciliation data
                activeShift.ExpectedEndingCash = expectedEndingCash;
                activeShift.ActualEndingCash = request.ActualEndingCash;
                activeShift.CashDiscrepancy = discrepancy;
                activeShift.DiscrepancyReason = request.DiscrepancyReason;
                activeShift.EndTime = DateTime.Now;
                activeShift.Status = "Closed";

                await _shiftRepo.UpdateShiftAsync(activeShift);

                // 6. Nếu chênh lệch != 0, dữ liệu đối soát đã được persist trên WorkShift.
                // Không ghi vào InvoiceAuditLog vì đó là domain hóa đơn/supervisor bypass.
                if (discrepancy != 0)
                {
                    _logger.LogWarning(
                        "SHIFT_RECONCILIATION_DISCREPANCY | ShiftId={ShiftId} | StoreId={StoreId} | " +
                        "UserId={UserId} | Expected={Expected:N0}đ | Actual={Actual:N0}đ | " +
                        "Discrepancy={Discrepancy:N0}đ | Reason=\"{Reason}\"",
                        activeShift.ShiftId, storeId, userId,
                        expectedEndingCash, request.ActualEndingCash,
                        discrepancy, request.DiscrepancyReason ?? "N/A");
                }

                return ServiceResult.Success(
                    $"Đóng ca thành công! Doanh thu tiền mặt: {totalCashSales:N0}đ. " +
                    $"Kỳ vọng: {expectedEndingCash:N0}đ. Thực tế: {request.ActualEndingCash:N0}đ. " +
                    $"Chênh lệch: {discrepancy:N0}đ.");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure("Lỗi hệ thống khi đóng ca: " + ex.Message);
            }
        }

        public async Task<ServiceResult> CloseShiftByExceptionAsync(
            int userId,
            int storeId,
            int shiftId,
            CloseShiftExceptionRequestDto request)
        {
            try
            {
                if (request == null)
                    return ServiceResult.Failure("Thiếu dữ liệu đóng ca ngoại lệ.");

                var exceptionReason = request.ExceptionReason?.Trim();
                if (string.IsNullOrWhiteSpace(exceptionReason))
                    return ServiceResult.Failure("Vui lòng nhập lý do đóng ca ngoại lệ.");

                if (string.IsNullOrWhiteSpace(request.SupervisorPin))
                    return ServiceResult.Failure("Vui lòng nhập mã PIN supervisor/manager.");

                var activeShift = await _shiftRepo.GetActiveShiftAsync(userId, storeId);
                if (activeShift == null || activeShift.ShiftId != shiftId)
                    return ServiceResult.Failure("Không tìm thấy ca két tiền đang mở với ID này.");

                if (await _shiftRepo.HasOpenPosPaymentAsync(activeShift.ShiftId, storeId))
                {
                    return ServiceResult.Failure(
                        "Không thể đóng ca ngoại lệ. Đang có giao dịch thanh toán chưa hoàn tất. " +
                        "Vui lòng hoàn tất hoặc hủy giao dịch trước khi đóng ca.");
                }

                var supervisorResult = await _supervisorAuthService.VerifySupervisorPinAsync(
                    request.SupervisorPin, storeId);

                if (!supervisorResult.IsSuccess || supervisorResult.Data == null)
                    return ServiceResult.Failure(supervisorResult.Message);

                var offlineSummary = request.OfflineQueueSummary ?? new OfflineQueueSummaryDto();
                if (offlineSummary.OfflineOrderCount < 0 ||
                    offlineSummary.EstimatedTotal < 0 ||
                    offlineSummary.LocalCashTotal < 0)
                {
                    return ServiceResult.Failure("Tóm tắt đơn offline không hợp lệ.");
                }

                var totalCashSales = await _shiftRepo.GetTotalCashSalesAsync(activeShift.ShiftId);
                var expectedEndingCash = activeShift.StartingCash + totalCashSales;
                var discrepancy = request.ActualEndingCash - expectedEndingCash;

                if (discrepancy != 0 && string.IsNullOrWhiteSpace(request.DiscrepancyReason))
                {
                    return ServiceResult.Failure($"Phát hiện chênh lệch {discrepancy:N0}đ. Vui lòng nhập lý do chênh lệch.");
                }

                var closedAt = DateTime.Now;
                activeShift.ExpectedEndingCash = expectedEndingCash;
                activeShift.ActualEndingCash = request.ActualEndingCash;
                activeShift.CashDiscrepancy = discrepancy;
                activeShift.DiscrepancyReason = request.DiscrepancyReason;
                activeShift.EndTime = closedAt;
                activeShift.Status = "Closed";
                activeShift.IsExceptionClosed = true;
                activeShift.ExceptionCloseReason = exceptionReason;
                activeShift.ExceptionClosedByStaffId = supervisorResult.Data.SupervisorStaffId;
                activeShift.ExceptionClosedAt = closedAt;
                activeShift.OfflineOrderCountAtClose = offlineSummary.OfflineOrderCount;
                activeShift.OfflineEstimatedTotalAtClose = offlineSummary.EstimatedTotal;
                activeShift.OfflineCashTotalAtClose = offlineSummary.LocalCashTotal;
                activeShift.RequiresReconciliation = true;

                await _shiftRepo.UpdateShiftAsync(activeShift);

                return ServiceResult.Success(
                    $"Đóng ca ngoại lệ thành công. Ca cần đối soát lại sau khi các đơn offline đồng bộ. " +
                    $"Offline chưa sync: {offlineSummary.OfflineOrderCount} đơn, " +
                    $"ước tính {offlineSummary.EstimatedTotal:N0}đ.");
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure("Lỗi hệ thống khi đóng ca ngoại lệ: " + ex.Message);
            }
        }
    }
}
