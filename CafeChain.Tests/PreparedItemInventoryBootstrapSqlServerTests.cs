using CafeChain.Application.Services.Admin.StoreInventories;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.Production;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Moq;
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

    [Fact]
    public async Task ProductionCapability_PersistsExplicitSelectedStoreAndZeroInventory()
    {
        await using var context = CreateContext();
        var permissions = new Mock<IAdminPermissionService>();
        permissions.Setup(x => x.HasPermissionAsync(_accountId, It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync((int accountId, string permissionCode, int? storeId) =>
                ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
                {
                    AccountId = accountId,
                    PermissionCode = permissionCode,
                    TargetStoreId = storeId,
                    Allowed = permissionCode == PermissionConstants.PreparedItemUpdate
                        || permissionCode == PermissionConstants.ProductionOrderPlan && storeId == _storeId,
                    ScopeAllowed = !storeId.HasValue || storeId == _storeId
                }));
        var service = new PreparedItemProductionCapabilityService(
            context,
            permissions.Object,
            new PreparedItemInventoryBootstrapService(context));

        var global = await service.SetGlobalProductionAsync(_accountId, 471, _preparedItemId, true, null);
        var store = await service.SetStoreProductionAsync(
            _accountId, 471, _storeId, _preparedItemId, true, null);

        Assert.True(global.IsSuccess, global.Message);
        Assert.True(store.IsSuccess, store.Message);
        await using var verify = CreateContext();
        var globalRow = await verify.InventoryItemSourceCapabilities.AsNoTracking()
            .SingleAsync(x => x.PreparedItemId == _preparedItemId);
        Assert.True(globalRow.CanProduce);
        Assert.False(globalRow.CanPurchase);
        Assert.Single(await verify.StoreProductionCapabilities.AsNoTracking()
            .Where(x => x.StoreId == _storeId && x.PreparedItemId == _preparedItemId && x.Active)
            .ToListAsync());
        var inventory = await verify.StoreInventories.AsNoTracking()
            .SingleAsync(x => x.StoreId == _storeId && x.PreparedItemId == _preparedItemId);
        Assert.Equal(0m, inventory.AvailableQty);
        Assert.Equal(0m, inventory.ReservedQty);
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
