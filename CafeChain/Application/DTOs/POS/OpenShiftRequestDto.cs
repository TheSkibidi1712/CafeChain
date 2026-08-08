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
        /// OTP challenge đã xác nhận cho ngữ cảnh ngoài lịch. Mở ca trễ từ ngưỡng Manager dùng approval riêng.
        /// </summary>
        public Guid? OtpChallengePublicId { get; set; }
        public Guid? LateOpenApprovalPublicId { get; set; }

        /// <summary>Lý do mở trễ hoặc ngoài lịch đã được backend xác nhận tại StaffHub.</summary>
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

        /// <summary>
        /// Server-only durable POS access session extracted from the validated JWT.
        /// The opened WorkShift is bound to this session in the same database transaction.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public Guid? PosAccessSessionId { get; set; }
    }

    public sealed class OpenShiftAssessmentRequestDto
    {
        public string PosTerminalId { get; set; } = string.Empty;
    }

    public sealed class OpenShiftAssessmentDto
    {
        public string? RecommendedAction { get; set; }
        public string OpenContext { get; set; } = string.Empty;
        public int? SourceStaffShiftId { get; set; }
        public DateTime? PlannedStartUtc { get; set; }
        public DateTime? PlannedEndUtc { get; set; }
        public int MinutesLate { get; set; }
        public int MinutesEarly { get; set; }
        public bool ReasonRequired { get; set; }
        public bool ApprovalRequired { get; set; }
        public bool ManagerApprovalRequired { get; set; }
        public int ManagerApprovalFromMinutes { get; set; }
        public int ScheduledApprovalMaxLateMinutes { get; set; }
        public bool CanManagerApproveAsScheduled { get; set; }
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
        public int ResponsibleStaffId { get; set; }
        public string? ResponsibleStaffName { get; set; }
        public bool IsOwnedByRequester { get; set; }
        public string RecommendedAction { get; set; } = string.Empty;
    }

    public sealed class PosTerminalOptionDto
    {
        public string TerminalId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
