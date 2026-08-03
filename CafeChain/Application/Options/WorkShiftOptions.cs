namespace CafeChain.Application.Options;

public sealed class WorkShiftOptions
{
    public const string SectionName = "WorkShift";

    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public int EarlyOpenMinutes { get; set; } = 30;
    public int LateReasonAfterMinutes { get; set; } = 15;
    public int LateApprovalAfterMinutes { get; set; } = 30;
    public int PostEndGraceMinutes { get; set; } = 30;
    public int OutsideScheduleDurationHours { get; set; } = 6;
    public int MinimumReasonLength { get; set; } = 10;
    public int MaximumReasonLength { get; set; } = 500;
    public int DeduplicationRetentionHours { get; set; } = 24;
    public int ProcessingLeaseMinutes { get; set; } = 2;

    public TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }
}
