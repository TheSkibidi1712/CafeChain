using System.Text.RegularExpressions;
using CafeChain.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class DashboardAnalyticsSqlServerTests : IAsyncLifetime
{
    private const string Database = "CafeChain_DashboardAnalyticsTests";
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
        await context.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

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
        Assert.True(Convert.ToInt64(await command.ExecuteScalarAsync()) >= 46);
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

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "CafeChain")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
