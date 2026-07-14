using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Controllers.Api.v1;
using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Controllers.Api.v1
{
    /// <summary>
    /// POS WorkShift APIs — Open, Close, Current shift.
    /// Reuses existing IWorkShiftService business logic.
    /// StoreId/StaffId từ JWT Claims (PosApiController base).
    /// </summary>
    [Route("api/v1/pos/shifts")]
    public class POSShiftController : PosApiController
    {
        private readonly IWorkShiftService _shiftService;
        private readonly IWorkShiftRepository _shiftRepo;
        private readonly IPOSOrderRepository _posRepo;
        private readonly AppDbContext _context;
        private readonly ILogger<POSShiftController> _logger;

        public POSShiftController(
            IWorkShiftService shiftService,
            IWorkShiftRepository shiftRepo,
            IPOSOrderRepository posRepo,
            AppDbContext context,
            ILogger<POSShiftController> logger)
        {
            _shiftService = shiftService;
            _shiftRepo = shiftRepo;
            _posRepo = posRepo;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// POST /api/v1/pos/shifts/open
        /// Mở ca két tiền mới. StoreId/StaffId lấy từ JWT.
        /// </summary>
        [HttpPost("open")]
        public async Task<IActionResult> OpenShift([FromBody] OpenShiftRequestDto request)
        {
            var result = await _shiftService.OpenShiftAsync(CurrentStaffId, CurrentStoreId, request);

            if (!result.IsSuccess)
            {
                // LATE_OPENING_REQUIRES_OTP → 403 Forbidden (cần OTP online)
                if (result.Message?.Contains("LATE_OPENING_REQUIRES_OTP") == true
                    || result.ErrorCode == "LATE_OPENING_REQUIRES_OTP")
                {
                    return StatusCode(403, new
                    {
                        success = false,
                        message = result.Message,
                        errorCode = result.ErrorCode
                    });
                }

                // Ca đang mở → 409 Conflict
                if (result.Message?.Contains("chưa được đóng") == true)
                    return Conflict(new { success = false, message = result.Message });

                return BadRequest(new { success = false, message = result.Message, errorCode = result.ErrorCode });
            }

            // Fetch the newly created shift to return summary
            var shift = await _shiftService.GetActiveShiftAsync(CurrentStaffId, CurrentStoreId);
            var summary = MapToSummary(shift!, 0, 0, 0);

            return StatusCode(201, summary);
        }

        /// <summary>
        /// POST /api/v1/pos/shifts/{id}/close
        /// Đóng ca két tiền + đối soát tiền mặt.
        /// </summary>
        [HttpPost("{id}/close")]
        public async Task<IActionResult> CloseShift(int id, [FromBody] CloseShiftRequestDto request)
        {
            // Verify shift belongs to current user/store
            var activeShift = await _shiftService.GetActiveShiftAsync(CurrentStaffId, CurrentStoreId);
            if (activeShift == null || activeShift.ShiftId != id)
            {
                return NotFound(new { success = false, message = "Không tìm thấy ca két tiền đang mở với ID này." });
            }

            var result = await _shiftService.CloseShiftAsync(CurrentStaffId, CurrentStoreId, request);

            if (!result.IsSuccess)
            {
                // Include errorCode so POS UI can open OTP panel on OTP_REQUIRED (#90/#91).
                return BadRequest(new { success = false, message = result.Message, errorCode = result.ErrorCode });
            }

            // Refresh shift data after close
            var closedShift = await _context.WorkShifts
                .AsNoTracking()
                .FirstOrDefaultAsync(ws => ws.ShiftId == id);

            if (closedShift == null)
                return Ok(new { success = true, message = result.Message });

            var totalCash = await _shiftRepo.GetTotalCashSalesAsync(id);
            var totalBanking = await _posRepo.GetTotalSalesByPaymentMethodAsync(id, 2); // 2 = Banking
            var totalOrders = await _posRepo.GetCompletedOrderCountAsync(id);

            var summary = MapToSummary(closedShift, totalCash, totalBanking, totalOrders);
            return Ok(summary);
        }

        /// <summary>
        /// POST /api/v1/pos/shifts/{id}/close-exception
        /// Đóng ca ngoại lệ bằng OTP phê duyệt (online) khi còn Offline Order local chưa Sync.
        /// </summary>
        [HttpPost("{id}/close-exception")]
        public async Task<IActionResult> CloseShiftByException(int id, [FromBody] CloseShiftExceptionRequestDto request)
        {
            var result = await _shiftService.CloseShiftByExceptionAsync(
                CurrentStaffId, CurrentStoreId, id, request);

            if (!result.IsSuccess)
            {
                return BadRequest(new { success = false, message = result.Message, errorCode = result.ErrorCode });
            }

            var closedShift = await _context.WorkShifts
                .AsNoTracking()
                .FirstOrDefaultAsync(ws => ws.ShiftId == id && ws.StoreId == CurrentStoreId);

            if (closedShift == null)
                return Ok(new { success = true, message = result.Message });

            var totalCash = await _shiftRepo.GetTotalCashSalesAsync(id);
            var totalBanking = await _posRepo.GetTotalSalesByPaymentMethodAsync(id, 2);
            var totalOrders = await _posRepo.GetCompletedOrderCountAsync(id);

            var summary = MapToSummary(closedShift, totalCash, totalBanking, totalOrders);
            return Ok(summary);
        }

        /// <summary>
        /// GET /api/v1/pos/shifts/current
        /// Trả ca đang mở. Nếu không có → { status: "NoActiveShift" }
        /// </summary>
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentShift()
        {
            var shift = await _shiftService.GetActiveShiftAsync(CurrentStaffId, CurrentStoreId);

            if (shift == null)
            {
                return Ok(new ShiftSummaryDto { Status = "NoActiveShift" });
            }

            var totalCash = await _shiftRepo.GetTotalCashSalesAsync(shift.ShiftId);
            var totalBanking = await _posRepo.GetTotalSalesByPaymentMethodAsync(shift.ShiftId, 2);
            var totalOrders = await _posRepo.GetCompletedOrderCountAsync(shift.ShiftId);

            var summary = MapToSummary(shift, totalCash, totalBanking, totalOrders);
            return Ok(summary);
        }

        // ============================================================
        // PRIVATE: Map WorkShift entity → ShiftSummaryDto
        // ============================================================
        private ShiftSummaryDto MapToSummary(
            CafeChain.Models.Stores.WorkShift shift,
            decimal totalCashSales,
            decimal totalBankingSales,
            int totalOrders)
        {
            return new ShiftSummaryDto
            {
                ShiftId = shift.ShiftId,
                StoreId = shift.StoreId,
                StaffName = shift.User?.FullName,
                StartTime = shift.StartTime,
                EndTime = shift.EndTime,
                StartingCash = shift.StartingCash,
                ExpectedEndingCash = shift.ExpectedEndingCash,
                ActualEndingCash = shift.ActualEndingCash,
                CashDiscrepancy = shift.CashDiscrepancy,
                IsExceptionClosed = shift.IsExceptionClosed,
                ExceptionCloseReason = shift.ExceptionCloseReason,
                ExceptionClosedByStaffId = shift.ExceptionClosedByStaffId,
                ExceptionClosedAt = shift.ExceptionClosedAt,
                OfflineOrderCountAtClose = shift.OfflineOrderCountAtClose,
                OfflineEstimatedTotalAtClose = shift.OfflineEstimatedTotalAtClose,
                OfflineCashTotalAtClose = shift.OfflineCashTotalAtClose,
                RequiresReconciliation = shift.RequiresReconciliation,
                HasLateOfflineSync = shift.HasLateOfflineSync,
                LateOfflineSyncCount = shift.LateOfflineSyncCount,
                LastLateOfflineSyncedAt = shift.LastLateOfflineSyncedAt,
                TotalCashSales = totalCashSales,
                TotalBankingSales = totalBankingSales,
                TotalOrders = totalOrders,
                Status = shift.Status
            };
        }
    }
}
