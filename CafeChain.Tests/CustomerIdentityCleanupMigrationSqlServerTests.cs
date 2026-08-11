using CafeChain.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class CustomerIdentityCleanupMigrationSqlServerTests : IAsyncLifetime
{
    private const string Database = "CafeChain_CustomerIdentityCleanupTests";
    private const string InitialMigration = "20260810115400_InitialCreate";
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
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Upgrade_removes_only_legacy_demo_identity_and_keeps_real_customer_records()
    {
        await using var context = CreateContext();
        var migrator = context.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(InitialMigration);

        await context.Database.ExecuteSqlRawAsync(
            """
            SET IDENTITY_INSERT dbo.Roles ON;
            INSERT dbo.Roles(RoleId,Name,Active,IsStoreLevel,CreatedAt)
            VALUES(7,N'Khách hàng',1,0,'2026-01-01');
            SET IDENTITY_INSERT dbo.Roles OFF;

            SET IDENTITY_INSERT dbo.Accounts ON;
            INSERT dbo.Accounts(AccountId,Email,PasswordHash,Active,RequiresPasswordChange,CreatedAt,FailedLoginAttempts)
            VALUES
              (7,N'khachhang@gmail.com',N'x',1,1,'2025-01-01',0),
              (700,N'real-customer@test.local',N'x',1,1,'2026-08-11',0);
            SET IDENTITY_INSERT dbo.Accounts OFF;

            SET IDENTITY_INSERT dbo.Customers ON;
            INSERT dbo.Customers
              (CustomerId,AccountId,CustomerCode,FullName,Category,Active,IsDeleted,CreatedAt,TotalSpent,TotalOrders,CurrentPoints)
            VALUES
              (1,7,N'CUS000111',N'Khách Hàng Mới',2,1,0,'2025-01-01',0,0,0),
              (700,700,N'REAL-CUSTOMER-700',N'Khách hàng thật',2,1,0,'2026-08-11',0,0,0);
            SET IDENTITY_INSERT dbo.Customers OFF;

            INSERT dbo.AccountRoles(AccountId,RoleId) VALUES(7,7),(700,7);
            INSERT dbo.CustomerPhones(CustomerId,Phone,IsDefault)
            VALUES(1,N'0900111222',0),(700,N'0900700700',1);
            """);

        await migrator.MigrateAsync();

        Assert.False(await context.Accounts.AsNoTracking()
            .AnyAsync(x => x.Email == "khachhang@gmail.com"));
        Assert.False(await context.Customers.AsNoTracking()
            .AnyAsync(x => x.CustomerCode == "CUS000111"));
        Assert.False(await context.Roles.AsNoTracking()
            .AnyAsync(x => x.Name == "Khách hàng"));

        var realCustomer = await context.Customers.AsNoTracking()
            .Include(x => x.Account)
            .ThenInclude(x => x!.AccountRoles)
            .Include(x => x.CustomerPhones)
            .SingleAsync(x => x.CustomerCode == "REAL-CUSTOMER-700");
        Assert.Equal("real-customer@test.local", realCustomer.Account!.Email);
        Assert.Empty(realCustomer.Account.AccountRoles);
        Assert.Contains(realCustomer.CustomerPhones, x => x.Phone == "0900700700");
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(ConnectionString)
        .Options);
}
