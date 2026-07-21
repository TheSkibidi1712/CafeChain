using System.Text.RegularExpressions;
using CafeChain.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class DashboardAnalyticsSqlServerTests : IAsyncLifetime
{
    private const string Database = "CafeChain_DashboardV13_Verify_20260721";
    private static string ConnectionString => SqlServerTestConnection.Create(Database);

    public async Task InitializeAsync()
    {
        await using var master = new SqlConnection(SqlServerTestConnection.MasterConnectionString());
        await master.OpenAsync();
        await using (var command = master.CreateCommand())
        {
            command.CommandText = $"IF DB_ID(N'{Database}') IS NULL CREATE DATABASE [{Database}];";
            await command.ExecuteNonQueryAsync();
        }
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Analytics_script_runs_twice_and_all_contracts_exist()
    {
        var script = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "Scripts",
            "20260717_DashboardAnalyticsStoredProcedures.idempotent.sql"));
        await ExecuteBatchesAsync(script);
        await ExecuteBatchesAsync(script);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT_BIG(1) FROM sys.procedures WHERE name LIKE 'usp[_]%' OR name LIKE 'sp[_]%';";
        Assert.Equal(45L, Convert.ToInt64(await command.ExecuteScalarAsync()));
        command.CommandText = "SELECT COUNT_BIG(1) FROM sys.procedures WHERE name='sp_Top_Customers';";
        Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task Empty_database_procedures_return_stable_zero_or_empty_results()
    {
        var script = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "Scripts",
            "20260717_DashboardAnalyticsStoredProcedures.idempotent.sql"));
        await ExecuteBatchesAsync(script);
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "dbo.usp_Dashboard_NetSalesTrend";
        command.CommandType = System.Data.CommandType.StoredProcedure;
        command.Parameters.AddWithValue("@FromDate", new DateTime(2026, 1, 1));
        command.Parameters.AddWithValue("@ToDate", new DateTime(2026, 1, 3));
        command.Parameters.AddWithValue("@StoreIds", "1");
        command.Parameters.AddWithValue("@Granularity", "Day");
        command.Parameters.AddWithValue("@Top", 10);
        await using var reader = await command.ExecuteReaderAsync();
        var count = 0;
        while (await reader.ReadAsync()) count++;
        Assert.Equal(3, count);
    }

    [Theory]
    [InlineData("Hour", "2026-01-01", "2026-01-01", 24)]
    [InlineData("Day", "2026-01-01", "2026-01-03", 3)]
    [InlineData("Week", "2026-01-01", "2026-01-15", 3)]
    [InlineData("Month", "2026-01-01", "2026-03-31", 3)]
    public async Task Net_sales_trend_returns_one_zero_filled_row_per_requested_bucket(
        string granularity,
        string from,
        string to,
        int expectedRows)
    {
        var script = File.ReadAllText(Path.Combine(FindRepoRoot(), "CafeChain", "Scripts",
            "20260717_DashboardAnalyticsStoredProcedures.idempotent.sql"));
        await ExecuteBatchesAsync(script);
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "dbo.usp_Dashboard_NetSalesTrend";
        command.CommandType = System.Data.CommandType.StoredProcedure;
        command.Parameters.AddWithValue("@FromDate", DateTime.Parse(from, System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@ToDate", DateTime.Parse(to, System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@StoreIds", "1");
        command.Parameters.AddWithValue("@Granularity", granularity);
        command.Parameters.AddWithValue("@Top", 10);

        var rows = 0;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows++;
            Assert.Equal(0L, Convert.ToInt64(reader.GetValue(reader.GetOrdinal("TotalOrders"))));
            Assert.Equal(0m, Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("NetSales"))));
        }

        Assert.Equal(expectedRows, rows);
    }

    [Fact]
    public async Task SeedAll_v13_runs_twice_and_exercises_all_dashboard_contracts()
    {
        var root = FindRepoRoot();
        var seed = File.ReadAllText(Path.Combine(root, "CafeChain", "Scripts", "SeedAll.sql"))
            .Replace("USE [CafeChain];", $"USE [{Database}];", StringComparison.Ordinal)
            .Replace("IF UPPER(DB_NAME()) <> N'CAFECHAIN'",
                $"IF UPPER(DB_NAME()) <> N'{Database.ToUpperInvariant()}'", StringComparison.Ordinal);
        var analytics = File.ReadAllText(Path.Combine(root, "CafeChain", "Scripts",
            "20260717_DashboardAnalyticsStoredProcedures.idempotent.sql"));

        await ExecuteBatchesAsync(seed);
        await ExecuteBatchesAsync(seed);
        await ExecuteBatchesAsync(analytics);
        await ExecuteBatchesAsync(analytics);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.Orders WHERE Source=N'DEMO_DASHBOARD_V13';", 6L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.Payments p JOIN dbo.Orders o ON o.OrderId=p.OrderId WHERE o.Source=N'DEMO_DASHBOARD_V13' AND p.CashSessionId IS NOT NULL;", 0L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.WorkShifts WHERE StoreId=1 AND StartTime IN ('2026-01-15T06:00:00','2026-01-15T12:00:00','2026-01-16T06:00:00','2026-01-18T06:00:00');", 4L);

        await using (var command = AnalyticsCommand(connection, "dbo.usp_Workforce_ShiftStatus"))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            var statuses = new HashSet<string>(StringComparer.Ordinal);
            var sawCustomTime = false;
            var sawOvernight = false;
            while (await reader.ReadAsync())
            {
                statuses.Add(reader.GetString(reader.GetOrdinal("StatusCode")));
                var start = reader.GetDateTime(reader.GetOrdinal("PlannedStartAt"));
                var end = reader.GetDateTime(reader.GetOrdinal("PlannedEndAt"));
                sawCustomTime |= start == new DateTime(2026, 1, 15, 13, 0, 0)
                                 && end == new DateTime(2026, 1, 15, 17, 30, 0);
                sawOvernight |= reader.GetBoolean(reader.GetOrdinal("IsOvernight"))
                                && end.Date > start.Date;
            }
            Assert.All(statuses, status => Assert.Contains(status, new[] { "SCHEDULED", "CANCELLED" }));
            Assert.Contains("SCHEDULED", statuses);
            Assert.Contains("CANCELLED", statuses);
            Assert.True(sawCustomTime);
            Assert.True(sawOvernight);
        }

        await using (var command = AnalyticsCommand(connection, "dbo.usp_Workforce_StaffPerformance"))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            var found = false;
            while (await reader.ReadAsync())
            {
                if (reader.GetInt32(reader.GetOrdinal("StaffId")) != 4) continue;
                found = true;
                Assert.Equal(4L, Convert.ToInt64(reader["WorkShiftCount"]));
                Assert.Equal(3L, Convert.ToInt64(reader["TotalOrders"]));
                Assert.Equal(157000m, Convert.ToDecimal(reader["NetSales"]));
            }
            Assert.True(found);
        }

        await using (var command = AnalyticsCommand(connection, "dbo.usp_Workforce_HourlyDemand"))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            var staffing = new Dictionary<int, long>();
            while (await reader.ReadAsync())
                staffing[Convert.ToInt32(reader["HourOfDay"])] = Convert.ToInt64(reader["ScheduledStaffCount"]);
            Assert.Equal(1L, staffing[1]);
            // Two scheduled morning rows exist after the consolidated seed; the cancelled row must not become a third.
            Assert.Equal(2L, staffing[6]);
            Assert.Equal(1L, staffing[23]);
        }

        await using (var command = AnalyticsCommand(connection, "dbo.usp_Dashboard_PaymentMethodMix"))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            var amounts = new Dictionary<string, decimal>(StringComparer.Ordinal);
            while (await reader.ReadAsync())
                amounts[Convert.ToString(reader["PaymentMethodCode"])!] = Convert.ToDecimal(reader["Amount"]);
            Assert.Equal(33000m, amounts["CASH"]);
            Assert.Equal(50000m, amounts["BANK"]);
            Assert.Equal(74000m, amounts["MOMO"]);
        }

        await using (var command = AnalyticsCommand(connection, "dbo.usp_Product_TopProducts"))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            var bacXiuSold = 0;
            var sawPartialCogs = false;
            while (await reader.ReadAsync())
            {
                if (Convert.ToString(reader["DrinkName"]) == "Bạc xỉu")
                    bacXiuSold = Convert.ToInt32(reader["TotalSold"]);
                sawPartialCogs |= Convert.ToString(reader["DataStatus"]) == "PARTIAL_COGS";
            }
            Assert.Equal(1, bacXiuSold);
            Assert.True(sawPartialCogs);
        }

        await using (var command = AnalyticsCommand(connection, "dbo.usp_Workforce_ShiftStatus"))
        {
            command.Parameters["@StoreIds"].Value = "999";
            await using var reader = await command.ExecuteReaderAsync();
            Assert.False(await reader.ReadAsync());
        }

        foreach (var procedure in CanonicalProcedures)
        {
            await using var command = AnalyticsCommand(connection, $"dbo.{procedure}");
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) { }
        }
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(ConnectionString).Options);

    private static async Task ExecuteBatchesAsync(string script)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                     .Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync();
        }
    }

    private static SqlCommand AnalyticsCommand(SqlConnection connection, string procedure)
    {
        var command = connection.CreateCommand();
        command.CommandText = procedure;
        command.CommandType = System.Data.CommandType.StoredProcedure;
        command.Parameters.AddWithValue("@FromDate", new DateTime(2026, 1, 1));
        command.Parameters.AddWithValue("@ToDate", new DateTime(2026, 1, 31));
        command.Parameters.AddWithValue("@StoreIds", "1");
        command.Parameters.AddWithValue("@Granularity", "Day");
        command.Parameters.AddWithValue("@Top", 20);
        return command;
    }

    private static async Task AssertScalarAsync(SqlConnection connection, string sql, long expected)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        Assert.Equal(expected, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    private static readonly string[] CanonicalProcedures =
    [
        "usp_Dashboard_NetSalesTrend", "usp_Dashboard_StoreRanking", "usp_Dashboard_PaymentMethodMix",
        "usp_Dashboard_OrderHeatmap", "usp_Dashboard_OperationalAlerts",
        "usp_Operations_WorkShiftCashDiscrepancy", "usp_Operations_WorkShiftSales", "usp_Operations_WorkShiftPaymentMix",
        "usp_Operations_OfflineReconciliationExceptions", "usp_Operations_HourlyOrders",
        "usp_Operations_WorkShiftTopDiscrepancies", "usp_Operations_WorkShiftKpis",
        "usp_Inventory_ShortageRisk", "usp_Inventory_MovementByType", "usp_Inventory_ThresholdRisk",
        "usp_Inventory_ReorderSuggestions", "usp_Inventory_WasteByStoreIngredient", "usp_Inventory_FifoLayerAge",
        "usp_Procurement_PurchaseOrderPipeline", "usp_Procurement_OverduePurchaseOrders", "usp_Procurement_SupplierQuality",
        "usp_Procurement_PurchasePriceTrend", "usp_Procurement_SpendBreakdown", "usp_Procurement_SupplierIssueMix",
        "usp_Product_TopProducts", "usp_Product_VolumeMarginMatrix", "usp_Product_SizeMargin",
        "usp_Product_TopToppings", "usp_Product_BomHealth", "usp_Product_HighConsumptionLowEfficiency",
        "usp_Workforce_ShiftStatus", "usp_Workforce_HourlyDemand", "usp_Workforce_StaffPerformance"
    ];

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "CafeChain")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
