using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Models.Operations
{
    /// <summary>
    /// One-time OTP email challenge for sensitive operational approvals (Phase 1: CASH_DIFFERENCE).
    /// </summary>
    public class OtpChallenge
    {
        [Key]
        public int OtpChallengeId { get; set; }

        public Guid PublicId { get; set; } = Guid.NewGuid();

        public int StoreId { get; set; }

        public int? WorkShiftId { get; set; }

        [MaxLength(100)]
        public string? TerminalId { get; set; }

        [MaxLength(200)]
        public string? RequestKey { get; set; }

        /// <summary>SHA-256 fingerprint; never stores the raw client IP.</summary>
        [MaxLength(64)]
        public string? ClientIpHash { get; set; }

        /// <summary>SHA-256 fingerprint derived from a server-observed device header/user agent.</summary>
        [MaxLength(64)]
        public string? DeviceFingerprintHash { get; set; }

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

        /// <summary>
        /// Data Protection ciphertext used only to let the selected approver review an
        /// active OTP from the private notification list. Never contains plaintext.
        /// Cleared as soon as the challenge leaves Pending.
        /// </summary>
        [MaxLength(2048)]
        public string? ProtectedOtpPayload { get; set; }

        /// <summary>SHA-256 hex fingerprint of canonical action payload (required for new challenges).</summary>
        [MaxLength(128)]
        public string PayloadFingerprint { get; set; } = string.Empty;

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

        /// <summary>SQL Server concurrency token (EnsureCreated maps as rowversion).</summary>
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        public virtual Store Store { get; set; } = null!;

        public virtual WorkShift? WorkShift { get; set; }

        public virtual Staff RequestedByStaff { get; set; } = null!;

        public virtual Staff ApproverStaff { get; set; } = null!;
    }
}
