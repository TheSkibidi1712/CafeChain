using System.Text.Json.Serialization;

namespace CafeChain.Application.DTOs.POS
{
    public class OtpRequestDto
    {
        public string ActionType { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public int? TargetId { get; set; }
        public int? WorkShiftId { get; set; }
        public string Reason { get; set; } = string.Empty;

        /// <summary>CASH_DIFFERENCE / CLOSE_SHIFT_EXCEPTION cash binding.</summary>
        public decimal ActualEndingCash { get; set; }

        /// <summary>CLOSE_SHIFT_EXCEPTION reason binding.</summary>
        public string? ExceptionReason { get; set; }

        /// <summary>Optional discrepancy note for exception close fingerprint.</summary>
        public string? DiscrepancyReason { get; set; }

        /// <summary>CLOSE_SHIFT_EXCEPTION offline queue binding.</summary>
        public OfflineQueueSummaryDto? OfflineQueueSummary { get; set; }

        /// <summary>OPEN_SHIFT_LATE starting cash binding.</summary>
        public decimal StartingCash { get; set; }

        public string? TerminalId { get; set; }
        public string? TerminalName { get; set; }
        public string? RequestKey { get; set; }

        public string? OldValueJson { get; set; }
        public string? NewValueJson { get; set; }

        [JsonIgnore]
        public string? ClientIpHash { get; set; }

        [JsonIgnore]
        public string? DeviceFingerprintHash { get; set; }
    }

    public class OtpVerifyDto
    {
        public Guid OtpChallengePublicId { get; set; }
        public string OtpCode { get; set; } = string.Empty;

        [JsonIgnore]
        public string? ClientIpHash { get; set; }

        [JsonIgnore]
        public string? DeviceFingerprintHash { get; set; }
    }

    public class OtpResendDto
    {
        public Guid OtpChallengePublicId { get; set; }

        [JsonIgnore]
        public string? ClientIpHash { get; set; }

        [JsonIgnore]
        public string? DeviceFingerprintHash { get; set; }
    }

    public class OtpCancelDto
    {
        public Guid OtpChallengePublicId { get; set; }
    }

    public class OtpChallengeResponseDto
    {
        public bool HasActiveChallenge { get; set; } = true;
        public Guid? OtpChallengePublicId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ActionType { get; set; }
        public string? OpenContext { get; set; }
        public string? TerminalId { get; set; }
        public string? TerminalName { get; set; }
        public string? Reason { get; set; }
        public string? RequestKey { get; set; }
        public int ExpiresInSeconds { get; set; }
        public int ResendAvailableInSeconds { get; set; }
        public int RemainingAttempts { get; set; }
        public bool Locked { get; set; }
        public DateTime? LockedUntilUtc { get; set; }
        public int RetryAfter { get; set; }
        public bool WasExistingActive { get; set; }
        public string? DeliveryStatus { get; set; }
        public string? MaskedRecipientEmail { get; set; }
    }

    public static class OtpDeliveryStatuses
    {
        public const string EmailSent = "EMAIL_SENT";
        public const string InternalNotificationOnly = "INTERNAL_NOTIFICATION_ONLY";
        public const string NoEligibleApprover = "NO_ELIGIBLE_APPROVER";
    }
}
