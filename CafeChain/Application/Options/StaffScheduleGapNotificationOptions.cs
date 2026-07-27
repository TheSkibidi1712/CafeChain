namespace CafeChain.Application.Options;

public sealed class StaffScheduleGapNotificationOptions
{
    public const string SectionName = "StaffScheduleNotifications";

    public bool Enabled { get; set; }
    public int InitialDelaySeconds { get; set; } = 60;
    public int IntervalMinutes { get; set; } = 60;
    public int LookaheadDays { get; set; } = 2;
    public int MaximumCandidatesPerAlert { get; set; } = 10;
    public int ReminderCooldownHours { get; set; } = 24;
}
