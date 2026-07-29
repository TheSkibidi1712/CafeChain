using System.Reflection;
using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.Application.Services.Admin.Dashboard;

namespace CafeChain.Tests;

public sealed class DashboardDataQualityContractTests
{
    public static IEnumerable<object[]> RowStatusCases()
    {
        yield return ["OK", "OK"];
        yield return ["AVAILABLE", "OK"];
        yield return ["NO_DATA", "NO_DATA"];
        yield return ["PARTIAL", "PARTIAL"];
        yield return ["PARTIAL_COGS", "PARTIAL_COGS"];
        yield return ["MISSING_CONFIG", "MISSING_CONFIG"];
        yield return ["THRESHOLD_NOT_CONFIGURED", "MISSING_CONFIG"];
        yield return ["ERROR", "ERROR"];
        yield return ["UNRECOGNIZED_STATUS", "PARTIAL"];
    }

    [Theory]
    [MemberData(nameof(RowStatusCases))]
    public void Row_status_is_normalized_fail_closed(string rowStatus, string expected)
    {
        var method = typeof(DashboardIntelligenceService).GetMethod(
            "EvaluateRowsStatus",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        var rows = new[]
        {
            new TopProductRow
            {
                DrinkId = 1,
                DrinkName = "Quality test",
                ProductRevenue = 100,
                DataStatus = rowStatus
            }
        };

        var actual = Assert.IsType<string>(method.Invoke(null, [rows, "AVAILABLE"]));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Mixed_error_and_ok_rows_are_partial()
    {
        var method = typeof(DashboardIntelligenceService).GetMethod(
            "EvaluateRowsStatus",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        var rows = new[]
        {
            new TopProductRow { DrinkId = 1, ProductRevenue = 100, DataStatus = "OK" },
            new TopProductRow { DrinkId = 2, ProductRevenue = 50, DataStatus = "ERROR" }
        };

        var actual = Assert.IsType<string>(method.Invoke(null, [rows, "AVAILABLE"]));

        Assert.Equal("PARTIAL", actual);
    }

    [Fact]
    public void Aggregate_status_preserves_single_degradation_kind()
    {
        var method = typeof(DashboardIntelligenceService).GetMethod(
            "AggregateDataStatus",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        Assert.Equal("PARTIAL_COGS", method.Invoke(null, [new[] { "OK", "PARTIAL_COGS" }]));
        Assert.Equal("MISSING_CONFIG", method.Invoke(null, [new[] { "OK", "MISSING_CONFIG" }]));
        Assert.Equal("PARTIAL", method.Invoke(null, [new[] { "OK", "PARTIAL_COGS", "MISSING_CONFIG" }]));
        Assert.Equal("ERROR", method.Invoke(null, [new[] { "ERROR", "NO_DATA" }]));
    }
}
