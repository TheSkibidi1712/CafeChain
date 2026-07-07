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
        /// Số tiền chênh lệch = ActualEndingCash - ExpectedEndingCash
        /// Dương = dư tiền, Âm = thiếu tiền. null = ca chưa đóng.
        /// </summary>
        public decimal? CashDiscrepancy { get; set; }

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

        /// <summary>
        /// Ca được đóng bằng ngoại lệ bởi supervisor/manager.
        /// </summary>
        public bool IsExceptionClosed { get; set; }

        /// <summary>
        /// Lý do đóng ngoại lệ do supervisor/manager nhập.
        /// </summary>
        [MaxLength(500)]
        public string? ExceptionCloseReason { get; set; }

        /// <summary>
        /// StaffId của supervisor/manager duyệt đóng ngoại lệ.
        /// </summary>
        public int? ExceptionClosedByStaffId { get; set; }

        /// <summary>
        /// Thời điểm supervisor/manager duyệt đóng ngoại lệ.
        /// </summary>
        public DateTime? ExceptionClosedAt { get; set; }

        /// <summary>
        /// Số Offline Order local chưa sync tại thời điểm đóng ngoại lệ.
        /// </summary>
        public int? OfflineOrderCountAtClose { get; set; }

        /// <summary>
        /// Tổng tiền ước tính của Offline Order local tại thời điểm đóng ngoại lệ.
        /// </summary>
        public decimal? OfflineEstimatedTotalAtClose { get; set; }

        /// <summary>
        /// Tổng tiền mặt local đã thu từ Offline Order tại thời điểm đóng ngoại lệ.
        /// </summary>
        public decimal? OfflineCashTotalAtClose { get; set; }

        /// <summary>
        /// Ca cần quản lý đối soát lại sau đóng ngoại lệ hoặc sync muộn.
        /// </summary>
        public bool RequiresReconciliation { get; set; }

        /// <summary>
        /// Có Offline Order sync vào sau khi WorkShift đã đóng.
        /// </summary>
        public bool HasLateOfflineSync { get; set; }

        /// <summary>
        /// Số Offline Order tạo mới sau khi WorkShift đã đóng.
        /// </summary>
        public int LateOfflineSyncCount { get; set; }

        /// <summary>
        /// Lần gần nhất Offline Order sync vào sau khi WorkShift đã đóng.
        /// </summary>
        public DateTime? LastLateOfflineSyncedAt { get; set; }

        /// <summary>
        /// Khóa ca két gắn cứng theo thiết bị POS Terminal (GUID từ browser localStorage)
        /// </summary>
        [MaxLength(100)]
        public string? PosTerminalId { get; set; }

        // ================= NAVIGATION =================
        public virtual Store Store { get; set; }
        public virtual Staff User { get; set; }
        public virtual Staff? ExceptionClosedByStaff { get; set; }
        public virtual PosTerminal? PosTerminal { get; set; }
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
