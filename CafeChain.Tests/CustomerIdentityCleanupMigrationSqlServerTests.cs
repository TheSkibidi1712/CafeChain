using CafeChain.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class CustomerIdentityCleanupMigrationSqlServerTests : IAsyncLifetime
{
    private readonly string _database = $"CafeChain_CustomerIdentityCleanupTests_{Guid.NewGuid():N}";
    private const string BaselineMigration = "20260815152712_InitialCreate";
    private string ConnectionString => SqlServerTestConnection.Create(_database);

    public async Task InitializeAsync()
    {
        await using var master = new SqlConnection(SqlServerTestConnection.MasterConnectionString());
        await master.OpenAsync();
        await using var command = master.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{_database}') IS NOT NULL DROP DATABASE [{_database}];";
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Baseline_contains_no_legacy_demo_identity_and_keeps_real_customer_records()
    {
        await using var context = CreateContext();
        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(BaselineMigration);

        Assert.False(await context.Accounts.AsNoTracking()
            .AnyAsync(x => x.Email == "khachhang@gmail.com"));
        Assert.False(await context.Customers.AsNoTracking()
            .AnyAsync(x => x.CustomerCode == "CUS000111"));

        await context.Database.ExecuteSqlRawAsync(
            """
            SET IDENTITY_INSERT dbo.Accounts ON;
            INSERT dbo.Accounts(AccountId,Email,PasswordHash,Active,RequiresPasswordChange,CreatedAt,FailedLoginAttempts)
            VALUES(700,N'real-customer@test.local',N'x',1,1,'2026-08-11',0);
            SET IDENTITY_INSERT dbo.Accounts OFF;

            SET IDENTITY_INSERT dbo.Customers ON;
            INSERT dbo.Customers
              (CustomerId,AccountId,CustomerCode,FullName,Category,Active,IsDeleted,CreatedAt,TotalSpent,TotalOrders,CurrentPoints)
            VALUES(700,700,N'REAL-CUSTOMER-700',N'Khách hàng thật',2,1,0,'2026-08-11',0,0,0);
            SET IDENTITY_INSERT dbo.Customers OFF;

            INSERT dbo.CustomerPhones(CustomerId,Phone,IsDefault)
            VALUES(700,N'0900700700',1);
            """);

        await migrator.MigrateAsync();

        var realCustomer = await context.Customers.AsNoTracking()
            .Include(x => x.Account)
            .ThenInclude(x => x!.AccountRoles)
            .Include(x => x.CustomerPhones)
            .SingleAsync(x => x.CustomerCode == "REAL-CUSTOMER-700");
        Assert.Equal("real-customer@test.local", realCustomer.Account!.Email);
        Assert.Empty(realCustomer.Account.AccountRoles);
        Assert.Contains(realCustomer.CustomerPhones, x => x.Phone == "0900700700");
    }

    private AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(ConnectionString)
        .Options);
}
