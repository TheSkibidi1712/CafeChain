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

        public decimal StartingCash { get; set; }
        public decimal ExpectedEndingCash { get; set; }
        public decimal? ActualEndingCash { get; set; }
        public decimal? CashDiscrepancy { get; set; }

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
