using CafeChain.Application.Interfaces.Systems;

namespace CafeChain.Application.Services.Systems;

public sealed class BusinessDateService : IBusinessDateService
{
    private readonly TimeProvider _timeProvider;
    public TimeZoneInfo TimeZone { get; }

    public BusinessDateService(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        TimeZone = ResolveVietnamTimeZone();
    }

    public DateTime Today => TimeZoneInfo.ConvertTime(
        _timeProvider.GetUtcNow(), TimeZone).Date;

    public DateTime ToBusinessDate(DateTime utcInstant) => TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc), TimeZone).Date;

    public DateTime ToBusinessTime(DateTime utcInstant) => TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc), TimeZone);

    public (DateTime LocalFrom, DateTime LocalToExclusive) GetLegacyLocalInterval(DateTime businessDate)
    {
        var from = DateTime.SpecifyKind(businessDate.Date, DateTimeKind.Unspecified);
        return (from, from.AddDays(1));
    }

    public (DateTime UtcFrom, DateTime UtcToExclusive) GetUtcInterval(DateTime businessDate)
    {
        var local = DateTime.SpecifyKind(businessDate.Date, DateTimeKind.Unspecified);
        return (TimeZoneInfo.ConvertTimeToUtc(local, TimeZone),
            TimeZoneInfo.ConvertTimeToUtc(local.AddDays(1), TimeZone));
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone(
            "Asia/Ho_Chi_Minh", TimeSpan.FromHours(7), "Vietnam", "Vietnam");
    }
}
