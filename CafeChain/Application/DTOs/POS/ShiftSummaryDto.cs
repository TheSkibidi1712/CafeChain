namespace CafeChain.Application.DTOs.POS
{
    /// <summary>
    /// Response DTO cho GET /api/v1/pos/shifts/current + POST open/close
    /// </summary>
    public class ShiftSummaryDto
    {
        /// <summary>WorkShift.ShiftId — null nếu không có ca mở</summary>
        public int? ShiftId { get; set; }

        public int StoreId { get; set; }
        public string? StaffName { get; set; }

        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime? StartTimeUtc { get; set; }
        public DateTime? EndTimeUtc { get; set; }
        public DateTime? BusinessDate { get; set; }
        public int? SourceStaffShiftId { get; set; }
        public string? OpenContext { get; set; }
        public DateTime? AutoCloseAtUtc { get; set; }
        public DateTime? ExpiredAtUtc { get; set; }
        public DateTime? ClosingStartedAtUtc { get; set; }
        public DateTime ServerNowUtc { get; set; }
        public string? CloseType { get; set; }
        public int? ClosedByStaffId { get; set; }
        public string? CloseReason { get; set; }
        public string? RowVersion { get; set; }

        public decimal StartingCash { get; set; }
        public decimal ExpectedEndingCash { get; set; }
        public decimal? ActualEndingCash { get; set; }
        public decimal? CashDiscrepancy { get; set; }
        public bool IsExceptionClosed { get; set; }
        public string? ExceptionCloseReason { get; set; }
        public int? ExceptionClosedByStaffId { get; set; }
        public DateTime? ExceptionClosedAt { get; set; }
        public int? OfflineOrderCountAtClose { get; set; }
        public decimal? OfflineEstimatedTotalAtClose { get; set; }
        public decimal? OfflineCashTotalAtClose { get; set; }
        public bool RequiresReconciliation { get; set; }
        public bool HasLateOfflineSync { get; set; }
        public int LateOfflineSyncCount { get; set; }
        public DateTime? LastLateOfflineSyncedAt { get; set; }

        /// <summary>Tổng doanh thu tiền mặt trong ca</summary>
        public decimal TotalCashSales { get; set; }

        /// <summary>Tổng doanh thu chuyển khoản trong ca</summary>
        public decimal TotalBankingSales { get; set; }

        /// <summary>Tổng số đơn hàng trong ca</summary>
        public int TotalOrders { get; set; }

        /// <summary>"Open" | "Closed" | "NoActiveShift"</summary>
        public string Status { get; set; } = null!;
    }
}
