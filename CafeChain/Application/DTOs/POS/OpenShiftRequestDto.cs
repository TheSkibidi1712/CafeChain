namespace CafeChain.Application.DTOs.POS
{
    /// <summary>
    /// Request DTO cho POST /api/v1/pos/shifts/open
    /// StoreId và StaffId lấy từ JWT Claims — KHÔNG từ request body.
    /// </summary>
    public class OpenShiftRequestDto
    {
        public string RequestKey { get; set; } = string.Empty;

        /// <summary>Tiền lẻ đầu ca đặt vào két</summary>
        public decimal StartingCash { get; set; }

        /// <summary>GUID thiết bị POS (từ browser localStorage)</summary>
        public string? PosTerminalId { get; set; }

        /// <summary>
        /// OTP challenge đã duyệt khi mở ca trễ &gt; 30 phút.
        /// </summary>
        public Guid? OtpChallengePublicId { get; set; }

        /// <summary>Lý do mở ca trễ — bắt buộc khi OTP late-open (fingerprint).</summary>
        public string? LateOpeningReason { get; set; }

        /// <summary>Lý do mở trễ hoặc mở POS ngoài lịch.</summary>
        public string? Reason { get; set; }
    }

    public sealed class OpenShiftAssessmentRequestDto
    {
        public string PosTerminalId { get; set; } = string.Empty;
    }

    public sealed class OpenShiftAssessmentDto
    {
        public string OpenContext { get; set; } = string.Empty;
        public int? SourceStaffShiftId { get; set; }
        public DateTime? PlannedStartUtc { get; set; }
        public DateTime? PlannedEndUtc { get; set; }
        public int MinutesLate { get; set; }
        public bool ReasonRequired { get; set; }
        public bool ApprovalRequired { get; set; }
        public DateTime ServerNowUtc { get; set; }
        public DateTime? AutoCloseAtUtc { get; set; }
    }
}
