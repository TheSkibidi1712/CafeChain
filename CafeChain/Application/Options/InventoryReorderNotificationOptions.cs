namespace CafeChain.Application.Options;

public sealed class InventoryReorderNotificationOptions
{
    public const string SectionName = "InventoryReorderNotifications";
    public bool Enabled { get; set; } = true;
    public int InitialDelaySeconds { get; set; } = 30;
    public int IntervalMinutes { get; set; } = 30;
    public int AnalysisWindowDays { get; set; } = 30;
    public int ReorderReminderCooldownMinutes { get; set; } = 240;
}
