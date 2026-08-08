namespace CafeChain.Application.Interfaces.Systems;

public interface IBusinessDateService
{
    TimeZoneInfo TimeZone { get; }
    DateTime Today { get; }
    DateTime ToBusinessDate(DateTime utcInstant);
    (DateTime LocalFrom, DateTime LocalToExclusive) GetLegacyLocalInterval(DateTime businessDate);
    (DateTime UtcFrom, DateTime UtcToExclusive) GetUtcInterval(DateTime businessDate);
}
