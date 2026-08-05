namespace CafeChain.Application.DTOs.POS
{
    /// <summary>Public POS API contract after a StaffHub exchange.</summary>
    public sealed class OpenPosSessionRequestDto
    {
        public decimal StartingCash { get; set; }
    }

    /// <summary>
    /// Internal open command. Identity and special-open fields are populated from server context.
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

        /// <summary>
        /// Server-only exchange context id populated from the signed POS JWT.
        /// It is never accepted from the JSON request body.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public int? ExchangeContextId { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public int? AccountId { get; set; }
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
        public int MinutesEarly { get; set; }
        public bool ReasonRequired { get; set; }
        public bool ApprovalRequired { get; set; }
        public DateTime ServerNowUtc { get; set; }
        public DateTime? AutoCloseAtUtc { get; set; }
        public string? TerminalId { get; set; }
        public BlockingWorkShiftDto? BlockingWorkShift { get; set; }
    }

    public sealed class BlockingWorkShiftDto
    {
        public int WorkShiftId { get; set; }
        public string? TerminalId { get; set; }
        public string? TerminalName { get; set; }
        public DateTime StartTimeUtc { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? AutoCloseAtUtc { get; set; }
    }

    public sealed class PosTerminalOptionDto
    {
        public string TerminalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
