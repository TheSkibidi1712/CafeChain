namespace CafeChain.Application.DTOs.POS
{
    public class StaffNotificationItemDto
    {
        public int NotificationId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
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
