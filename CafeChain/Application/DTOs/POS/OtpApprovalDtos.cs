namespace CafeChain.Application.DTOs.POS
{
    public class OtpRequestDto
    {
        public string ActionType { get; set; } = string.Empty;
        public string TargetType { get; set; } = string.Empty;
        public int? TargetId { get; set; }
        public int? WorkShiftId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? OldValueJson { get; set; }
        public string? NewValueJson { get; set; }
    }

    public class OtpVerifyDto
    {
        public Guid OtpChallengePublicId { get; set; }
        public string OtpCode { get; set; } = string.Empty;
    }

    public class OtpResendDto
    {
        public Guid OtpChallengePublicId { get; set; }
    }

    public class OtpChallengeResponseDto
    {
        public Guid? OtpChallengePublicId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int ExpiresInSeconds { get; set; }
        public int ResendAvailableInSeconds { get; set; }
        public int RemainingAttempts { get; set; }
    }
}
