using CafeChain.Application.Services.POS;
using CafeChain.Models.Staffs;
using Xunit;

namespace CafeChain.Tests.POS;

public sealed class ScheduleIntervalResolverTests
{
    [Fact]
    public void Resolve_OvernightShift_EndsOnFollowingDate()
    {
        var schedule = CreateSchedule(
            new DateTime(2026, 8, 2),
            TimeSpan.FromHours(22),
            TimeSpan.FromHours(6));

        var interval = ScheduleIntervalResolver.Resolve(schedule);

        Assert.Equal(new DateTime(2026, 8, 2, 22, 0, 0), interval.StartLocal);
        Assert.Equal(new DateTime(2026, 8, 3, 6, 0, 0), interval.EndLocal);
        Assert.Equal(DateTimeKind.Unspecified, interval.StartLocal.Kind);
        Assert.Equal(DateTimeKind.Unspecified, interval.EndLocal.Kind);
    }

    [Fact]
    public void Resolve_CustomTimes_OverrideShiftTemplate()
    {
        var schedule = CreateSchedule(
            new DateTime(2026, 8, 2),
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(16));
        schedule.CustomStartTime = new TimeSpan(23, 30, 0);
        schedule.CustomEndTime = new TimeSpan(5, 15, 0);

        var interval = ScheduleIntervalResolver.Resolve(schedule);

        Assert.Equal(new DateTime(2026, 8, 2, 23, 30, 0), interval.StartLocal);
        Assert.Equal(new DateTime(2026, 8, 3, 5, 15, 0), interval.EndLocal);
    }

    [Fact]
    public void Resolve_EqualStartAndEnd_TreatsEndAsFollowingDate()
    {
        var schedule = CreateSchedule(
            new DateTime(2026, 8, 2),
            TimeSpan.FromHours(7),
            TimeSpan.FromHours(7));

        var interval = ScheduleIntervalResolver.Resolve(schedule);

        Assert.Equal(TimeSpan.FromDays(1), interval.EndLocal - interval.StartLocal);
    }

    [Fact]
    public void ToUtc_UsesConfiguredStoreTimezone()
    {
        var timezone = ResolveVietnamTimeZone();
        var local = new DateTime(2026, 8, 2, 23, 30, 0, DateTimeKind.Unspecified);

        var utc = ScheduleIntervalResolver.ToUtc(local, timezone);

        Assert.Equal(new DateTime(2026, 8, 2, 16, 30, 0, DateTimeKind.Utc), utc);
    }

    private static StaffShift CreateSchedule(DateTime workDate, TimeSpan start, TimeSpan end) => new()
    {
        WorkDate = workDate,
        Shift = new Shift
        {
            Name = "Ca kiểm thử",
            StartTime = start,
            EndTime = end,
            Active = true
        }
    };

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }

        throw new TimeZoneNotFoundException("Không tìm thấy timezone Việt Nam trên hệ điều hành kiểm thử.");
    }
}
