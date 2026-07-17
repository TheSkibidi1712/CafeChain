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
