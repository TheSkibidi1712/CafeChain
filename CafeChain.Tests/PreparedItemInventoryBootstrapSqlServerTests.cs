using CafeChain.Application.Services.Admin.StoreInventories;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class PreparedItemInventoryBootstrapSqlServerTests : IAsyncLifetime
{
    private const string Database = "CafeChain_PreparedItemBootstrap471Tests";
    private static string ConnectionString => SqlServerTestConnection.Create(Database);
    private int _accountId;
    private int _storeId;
    private int _preparedItemId;

    public async Task InitializeAsync()
    {
        await using (var master = new SqlConnection(SqlServerTestConnection.MasterConnectionString()))
        {
            await master.OpenAsync();
            await using var command = master.CreateCommand();
            command.CommandText = $"IF DB_ID(N'{Database}') IS NULL CREATE DATABASE [{Database}];";
            await command.ExecuteNonQueryAsync();
        }

        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        var now = new DateTime(2026, 8, 16, 8, 0, 0, DateTimeKind.Utc);
        var account = new Account
        {
            Email = "bootstrap471@cafechain.test",
            PasswordHash = "test",
            Active = true,
            CreatedAt = now
        };
        var store = new Store
        {
            Name = "Cua hang bootstrap SQL",
            Address = "Test",
            Phone = "000",
            Active = true,
            CreatedAt = now
        };
        var preparedItem = new PreparedItem
        {
            Code = "BTP-BOOTSTRAP-SQL-471",
            Name = "Cot tra bootstrap SQL",
            BaseUnitId = 1,
            Active = true
        };
        context.Accounts.Add(account);
        context.Stores.Add(store);
        context.PreparedItems.Add(preparedItem);
        await context.SaveChangesAsync();
        _accountId = account.AccountId;
        _storeId = store.StoreId;
        _preparedItemId = preparedItem.PreparedItemId;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ConcurrentPreparedItemBootstrap_CreatesOneCanonicalInventory()
    {
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var first = new PreparedItemInventoryBootstrapService(firstContext);
        var second = new PreparedItemInventoryBootstrapService(secondContext);

        var results = await Task.WhenAll(
            first.EnsureAsync(_storeId, _preparedItemId, _accountId, "ConcurrentOperationalization"),
            second.EnsureAsync(_storeId, _preparedItemId, _accountId, "ConcurrentOperationalization"));

        Assert.All(results, result => Assert.True(result.IsSuccess, result.Message));
        await using var verify = CreateContext();
        var row = Assert.Single(await verify.StoreInventories
            .AsNoTracking()
            .Where(x => x.StoreId == _storeId && x.PreparedItemId == _preparedItemId)
            .ToListAsync());
        Assert.Equal(0m, row.AvailableQty);
        Assert.Equal(0m, row.ReservedQty);
        Assert.Empty(await verify.InventoryTransactions.AsNoTracking().ToListAsync());
        Assert.Empty(await verify.InventoryCostLayers.AsNoTracking().ToListAsync());
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new AppDbContext(options);
    }
}
