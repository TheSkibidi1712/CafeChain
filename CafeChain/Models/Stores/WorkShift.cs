using CafeChain.Models.Staffs;
using CafeChain.Models.Orders;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        public DateTime StartTimeUtc { get; set; }

        /// <summary>
        /// Thời điểm đóng ca — null nếu ca đang mở
        /// </summary>
        public DateTime? EndTimeUtc { get; set; }

        /// <summary>Ngày nghiệp vụ tại múi giờ cửa hàng, không đổi khi qua nửa đêm.</summary>
        public DateTime BusinessDate { get; set; }

        /// <summary>Lịch dự kiến nguồn; null cho phiên ngoài lịch/legacy.</summary>
        public int? SourceStaffShiftId { get; set; }

        [MaxLength(32)]
        public string OpenContext { get; set; } = WorkShiftOpenContexts.Legacy;

        [MaxLength(500)]
        public string? OutsideScheduleReason { get; set; }

        public int? ApprovedByStaffId { get; set; }
        public DateTime? ApprovedAtUtc { get; set; }
        public DateTime? AutoCloseAtUtc { get; set; }
        public DateTime? ExpiredAtUtc { get; set; }
        public DateTime? ClosingStartedAtUtc { get; set; }

        [MaxLength(32)]
        public string? CloseType { get; set; }

        public int? ClosedByStaffId { get; set; }

        [MaxLength(500)]
        public string? CloseReason { get; set; }

        /// <summary>0=chưa cảnh báo, 1=30 phút, 2=10 phút, 3=1 phút, 4=hết hạn.</summary>
        public byte ExpiryWarningLevel { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

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
        [MaxLength(32)]
        public string Status { get; set; } = WorkShiftStatuses.Open;

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
        public DateTime? LastLateOfflineSyncedAtUtc { get; set; }

        /// <summary>
        /// Khóa ca két gắn cứng theo thiết bị POS Terminal (GUID từ browser localStorage)
        /// </summary>
        [MaxLength(100)]
        public string? PosTerminalId { get; set; }

        // ================= NAVIGATION =================
        public virtual Store Store { get; set; }
        public virtual Staff User { get; set; }
        public virtual Staff? ExceptionClosedByStaff { get; set; }
        public virtual Staff? ApprovedByStaff { get; set; }
        public virtual Staff? ClosedByStaff { get; set; }
        public virtual StaffShift? SourceStaffShift { get; set; }
        public virtual PosTerminal? PosTerminal { get; set; }
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

        // Compatibility aliases for modules that have not yet migrated their display-only code.
        [NotMapped]
        public DateTime StartTime
        {
            get => AsLocalCompatibilityTime(StartTimeUtc);
            set => StartTimeUtc = AsUtcStorageTime(value);
        }

        [NotMapped]
        public DateTime? EndTime
        {
            get => EndTimeUtc.HasValue ? AsLocalCompatibilityTime(EndTimeUtc.Value) : null;
            set => EndTimeUtc = value.HasValue ? AsUtcStorageTime(value.Value) : null;
        }

        [NotMapped]
        public DateTime? LastLateOfflineSyncedAt
        {
            get => LastLateOfflineSyncedAtUtc;
            set => LastLateOfflineSyncedAtUtc = value;
        }

        private static DateTime AsUtcStorageTime(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime()
        };

        private static DateTime AsLocalCompatibilityTime(DateTime value) =>
            DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime();
    }

    public static class WorkShiftStatuses
    {
        public const string Open = "OPEN";
        public const string Closing = "CLOSING";
        public const string ExpiredPendingClose = "EXPIRED_PENDING_CLOSE";
        public const string Closed = "CLOSED";
        public const string ReconciliationRequired = "RECONCILIATION_REQUIRED";

        public static readonly string[] ActiveResponsibility =
        {
            Open, Closing, ExpiredPendingClose
        };
    }

    public static class WorkShiftOpenContexts
    {
        public const string WithinSchedule = "WITHIN_SCHEDULE";
        public const string LateForSchedule = "LATE_FOR_SCHEDULE";
        public const string OutsideSchedule = "OUTSIDE_SCHEDULE";
        public const string Legacy = "LEGACY";
    }

    public static class WorkShiftCloseTypes
    {
        public const string Normal = "NORMAL";
        public const string Expired = "EXPIRED";
        public const string Exception = "EXCEPTION";
        public const string AutoEmptyShift = "AUTO_EMPTY_SHIFT";
    }
}
