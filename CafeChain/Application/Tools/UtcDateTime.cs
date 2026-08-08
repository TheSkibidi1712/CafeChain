namespace CafeChain.Application.Tools;

/// <summary>
/// SQL Server datetime2 does not preserve DateTime.Kind. Values passed here are
/// application UTC instants and must be marked as UTC before JSON serialization
/// or timezone conversion; their clock value must not be converted a second time.
/// </summary>
public static class UtcDateTime
{
    public static DateTime Normalize(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public static DateTime? Normalize(DateTime? value) =>
        value.HasValue ? Normalize(value.Value) : null;
}
