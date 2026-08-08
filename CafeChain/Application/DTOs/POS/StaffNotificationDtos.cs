namespace CafeChain.Application.DTOs.POS
{
    public class OperationalOtpNotificationDto
    {
        public Guid ChallengePublicId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string TerminalId { get; set; } = string.Empty;
        public string TerminalName { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public int RequestedByStaffId { get; set; }
        public string RequestedByName { get; set; } = string.Empty;
        public int ApproverStaffId { get; set; }
        public string ApproverName { get; set; } = string.Empty;
        public int? ConfirmedByStaffId { get; set; }
        public string? ConfirmedByName { get; set; }
        public DateTime SentAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime ServerNowUtc { get; set; }
        public string Status { get; set; } = string.Empty;
        public int RemainingSeconds { get; set; }
        public bool CanRevealOtp { get; set; }
        public bool CanContinueTerminalConfirmation { get; set; }
    }

    public class OtpRevealResultDto
    {
        public string Code { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime ServerNowUtc { get; set; }
    }

    public class ConfirmTerminalNotificationRequestDto
    {
        public string OtpCode { get; set; } = string.Empty;
        public string RequestKey { get; set; } = string.Empty;
    }

    public class StaffNotificationItemDto
    {
        public int NotificationId { get; set; }
        public int StoreId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Severity { get; set; } = "INFO";
        public bool IsResolved { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public int EntityId { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool EmailAttempted { get; set; }
        public bool EmailSent { get; set; }

        /// <summary>none | pending | sent | failed — never raw SMTP text.</summary>
        public string EmailDeliveryHint { get; set; } = "none";

        public string? TargetUrl { get; set; }
        public string? TargetActionLabel { get; set; }

        public OperationalOtpNotificationDto? OperationalOtp { get; set; }
    }

    public class StaffNotificationListDto
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public int UnreadCount { get; set; }
        public List<StaffNotificationItemDto> Items { get; set; } = new();
    }

    public class StaffNotificationUnreadCountDto
    {
        public int UnreadCount { get; set; }
    }

    public class StaffNotificationMarkReadResultDto
    {
        public int MarkedCount { get; set; }
    }
}
