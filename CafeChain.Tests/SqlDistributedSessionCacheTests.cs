using CafeChain.Extensions.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class SqlDistributedSessionCacheTests : IAsyncLifetime
{
    private const string Database = "CafeChain_DistributedSessionCacheTests";
    private static string ConnectionString => SqlServerTestConnection.Create(Database);

    public async Task InitializeAsync()
    {
        await using (var master = new SqlConnection(SqlServerTestConnection.MasterConnectionString()))
        {
            await master.OpenAsync();
            await using var createDatabase = master.CreateCommand();
            createDatabase.CommandText = $"IF DB_ID(N'{Database}') IS NULL CREATE DATABASE [{Database}];";
            await createDatabase.ExecuteNonQueryAsync();
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var createTable = connection.CreateCommand();
        createTable.CommandText =
            """
            DROP TABLE IF EXISTS dbo.SessionCache;
            CREATE TABLE dbo.SessionCache
            (
                Id nvarchar(449) NOT NULL CONSTRAINT PK_SessionCache PRIMARY KEY,
                Value varbinary(max) NOT NULL,
                ExpiresAtTime datetimeoffset NOT NULL,
                SlidingExpirationInSeconds bigint NULL,
                AbsoluteExpiration datetimeoffset NULL
            );
            CREATE INDEX IX_SessionCache_ExpiresAtTime ON dbo.SessionCache(ExpiresAtTime);
            """;
        await createTable.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Session_value_survives_rebuilding_the_application_service_provider()
    {
        const string key = "session-restart-regression";
        var expected = Guid.NewGuid().ToString("N");

        using (var first = BuildProvider())
        {
            await first.GetRequiredService<IDistributedCache>().SetStringAsync(
                key,
                expected,
                new DistributedCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromMinutes(30)
                });
        }

        using var second = BuildProvider();
        var actual = await second.GetRequiredService<IDistributedCache>().GetStringAsync(key);

        Assert.Equal(expected, actual);
    }

    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["SessionCache:SchemaName"] = "dbo",
                ["SessionCache:TableName"] = "SessionCache"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddCafeChainWeb(configuration);
        return services.BuildServiceProvider();
    }
}
