using CafeChain.Models.Staffs;
using CafeChain.Models.Orders;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Models.Stores
{
    /// <summary>
    /// Ca làm việc POS — Quản lý mở/đóng ca thu ngân
    /// </summary>
    public class WorkShift
    {
        [Key]
        public int ShiftId { get; set; }

        /// <summary>
        /// Cửa hàng thực hiện ca
        /// </summary>
        public int StoreId { get; set; }

        /// <summary>
        /// Nhân viên thu ngân mở ca (FK → Staff.StaffId)
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// Thời điểm mở ca
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Thời điểm đóng ca — null nếu ca đang mở
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Tiền lẻ đầu ca (tiền mặt được giao cho thu ngân)
        /// </summary>
        public decimal StartingCash { get; set; }

        /// <summary>
        /// Tiền mặt kỳ vọng cuối ca = StartingCash + tổng Cash Sales trong ca
        /// Hệ thống tính tự động
        /// </summary>
        public decimal ExpectedEndingCash { get; set; }

        /// <summary>
        /// Tiền mặt thực tế đếm được khi đóng ca (nhập thủ công)
        /// </summary>
        public decimal? ActualEndingCash { get; set; }

        /// <summary>
        /// Trạng thái ca: Open | Closed
        /// </summary>
        [MaxLength(20)]
        public string Status { get; set; } = "Open";

        /// <summary>
        /// Lý do chênh lệch tiền mặt khi đóng ca (nếu có)
        /// </summary>
        [MaxLength(500)]
        public string? DiscrepancyReason { get; set; }

        // ================= NAVIGATION =================
        public virtual Store Store { get; set; }
        public virtual Staff User { get; set; }
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
