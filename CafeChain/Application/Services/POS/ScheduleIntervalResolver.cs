using CafeChain.Models.Staffs;

namespace CafeChain.Application.Services.POS;

public static class ScheduleIntervalResolver
{
    public static (DateTime StartLocal, DateTime EndLocal) Resolve(StaffShift schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(schedule.Shift);

        var startTime = schedule.CustomStartTime ?? schedule.Shift.StartTime;
        var endTime = schedule.CustomEndTime ?? schedule.Shift.EndTime;
        var start = DateTime.SpecifyKind(schedule.WorkDate.Date.Add(startTime), DateTimeKind.Unspecified);
        var end = DateTime.SpecifyKind(schedule.WorkDate.Date.Add(endTime), DateTimeKind.Unspecified);
        if (end <= start)
            end = end.AddDays(1);
        return (start, end);
    }

    public static DateTime ToUtc(DateTime localUnspecified, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localUnspecified, DateTimeKind.Unspecified), timeZone);
}
