using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Models.Operations
{
    /// <summary>
    /// OTP email xác nhận thao tác vận hành nhạy cảm bởi Ca trưởng/Cửa hàng trưởng.
    /// </summary>
    public class OtpChallenge
    {
        [Key]
        public int OtpChallengeId { get; set; }

        public Guid PublicId { get; set; } = Guid.NewGuid();

        public int StoreId { get; set; }

        public int? WorkShiftId { get; set; }

        public int RequestedByStaffId { get; set; }

        public int ApproverStaffId { get; set; }

        [MaxLength(50)]
        public string ActionType { get; set; } = string.Empty;

        [MaxLength(50)]
        public string TargetType { get; set; } = string.Empty;

        public int? TargetId { get; set; }

        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(255)]
        public string OtpHash { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public DateTime? UsedAt { get; set; }

        public DateTime? LockedAt { get; set; }

        public DateTime? CancelledAt { get; set; }

        public int FailedAttempts { get; set; }

        public int ResendCount { get; set; }

        public DateTime LastSentAt { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public string? OldValueJson { get; set; }

        public string? NewValueJson { get; set; }

        public virtual Store Store { get; set; } = null!;

        public virtual WorkShift? WorkShift { get; set; }

        public virtual Staff RequestedByStaff { get; set; } = null!;

        public virtual Staff ApproverStaff { get; set; } = null!;
    }
}
