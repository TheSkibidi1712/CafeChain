using CafeChain.Data;
using CafeChain.Infrastrusture.Repositories.Admin.StoreInventories;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class InventoryHistorySqlServerIssue196Tests : IAsyncLifetime
{
    private const string Database = "CafeChain_InventoryHistoryTests";
    private static string ConnectionString => SqlServerTestConnection.Create(Database);

    public async Task InitializeAsync()
    {
        try
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
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"SQL Server integration environment unavailable for inventory history. " +
                $"Set {SqlServerTestConnection.EnvVarName}. Database={Database}. {ex.Message}",
                ex);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SqlServer_InventoryHistory_LoadsMixedIdentityTransactions()
    {
        var seed = await SeedMixedHistoryAsync();
        await using var context = CreateContext();
        var repository = new AdminStoreInventoryRepository(context);

        var (rows, total) = await repository.GetTransactionsByStoreIdsAsync(
            new List<int> { seed.StoreId }, seed.StoreId, 1, 20);

        Assert.Equal(3, total);
        Assert.Contains(rows, x => x.IngredientName == "SQL nguyên liệu lịch sử");
        Assert.Contains(rows, x => x.IngredientName == "SQL BTP lịch sử" && x.IdentityBadge == "BTP");
        Assert.Contains(rows, x => x.IngredientName == $"Công thức #{seed.RecipeId}"
                                   && x.IdentityBadge == "BTP legacy");
    }

    [Fact]
    public async Task SqlServer_InventoryHistory_NullReferencesDoNotFailProjection()
    {
        var seed = await SeedMixedHistoryAsync();
        await using var context = CreateContext();
        var repository = new AdminStoreInventoryRepository(context);

        var (rows, _) = await repository.GetTransactionsByStoreIdsAsync(
            new List<int> { seed.StoreId }, seed.StoreId, 1, 20);

        Assert.Contains(rows, x => x.UnitPrice == null
                                   && x.TotalAmount == null
                                   && x.InventoryDocumentId == null
                                   && x.InventoryTransferId == null
                                   && x.ReferenceOrderId == null
                                   && x.ReferenceType == "Giao dịch kho");
    }

    [Fact]
    public async Task SqlServer_InventoryHistory_PaginationIsStable()
    {
        var seed = await SeedMixedHistoryAsync();
        await using var context = CreateContext();
        var repository = new AdminStoreInventoryRepository(context);

        var (firstPage, _) = await repository.GetTransactionsByStoreIdsAsync(
            new List<int> { seed.StoreId }, seed.StoreId, 1, 1);
        var (secondPage, _) = await repository.GetTransactionsByStoreIdsAsync(
            new List<int> { seed.StoreId }, seed.StoreId, 2, 1);

        Assert.True(Assert.Single(firstPage).InventoryTransactionId
                    > Assert.Single(secondPage).InventoryTransactionId);
    }

    [Fact]
    public async Task SqlServer_InventoryHistory_StoreScopeDoesNotLeak()
    {
        var seed = await SeedMixedHistoryAsync();
        await using var context = CreateContext();
        var repository = new AdminStoreInventoryRepository(context);

        var (rows, total) = await repository.GetTransactionsByStoreIdsAsync(
            new List<int> { seed.StoreId }, seed.OtherStoreId, 1, 20);

        Assert.Empty(rows);
        Assert.Equal(0, total);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<SeedResult> SeedMixedHistoryAsync()
    {
        await using var context = CreateContext();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var unit = new Unit
        {
            UnitCode = "h" + suffix[..6],
            Name = "History unit " + suffix,
            Type = UnitType.KhoiLuong,
            Active = true
        };
        var store = Store("SQL Thủ Dầu Một history " + suffix);
        var otherStore = Store("SQL ngoài scope " + suffix);
        var ingredient = new Ingredient
        {
            Code = "ING-HISTORY-" + suffix,
            Name = "SQL nguyên liệu lịch sử",
            BaseUnit = unit,
            Active = true
        };
        var preparedItem = new PreparedItem
        {
            Code = "BTP-HISTORY-" + suffix,
            Name = "SQL BTP lịch sử",
            BaseUnit = unit,
            Active = true
        };
        var recipe = new Recipe
        {
            RecipeCode = "RECIPE-HISTORY-" + suffix,
            Name = "SQL công thức legacy lịch sử",
            Active = true,
            Status = "Active",
            YieldPercentage = 100m
        };

        context.AddRange(store, otherStore, ingredient, preparedItem, recipe);
        await context.SaveChangesAsync();

        var ingredientInventory = Inventory(store.StoreId, ingredientId: ingredient.IngredientId);
        var preparedInventory = Inventory(store.StoreId, preparedItemId: preparedItem.PreparedItemId);
        var legacyInventory = Inventory(store.StoreId, recipeId: recipe.RecipeId);
        var otherInventory = Inventory(otherStore.StoreId, ingredientId: ingredient.IngredientId);
        context.StoreInventories.AddRange(
            ingredientInventory,
            preparedInventory,
            legacyInventory,
            otherInventory);
        await context.SaveChangesAsync();

        var createdAt = new DateTime(2026, 7, 22, 1, 0, 0, DateTimeKind.Utc);
        context.InventoryTransactions.AddRange(
            Transaction(ingredientInventory.StoreInventoryId, InventoryTransactionTypeEnum.IMPORT, createdAt),
            Transaction(preparedInventory.StoreInventoryId, InventoryTransactionTypeEnum.PRODUCTION_IN, createdAt),
            Transaction(legacyInventory.StoreInventoryId, InventoryTransactionTypeEnum.IN_TRANSFER, createdAt),
            Transaction(otherInventory.StoreInventoryId, InventoryTransactionTypeEnum.ADJUSTMENT_IN, createdAt));
        await context.SaveChangesAsync();

        return new SeedResult(store.StoreId, otherStore.StoreId, recipe.RecipeId);
    }

    private static Store Store(string name) => new()
    {
        Name = name,
        Address = "Test",
        Phone = Guid.NewGuid().ToString("N")[..12],
        Active = true,
        CreatedAt = DateTime.UtcNow
    };

    private static StoreInventory Inventory(
        int storeId,
        int? ingredientId = null,
        int? preparedItemId = null,
        int? recipeId = null) => new()
    {
        StoreId = storeId,
        IngredientId = ingredientId,
        PreparedItemId = preparedItemId,
        RecipeId = recipeId,
        BtpIdentityState = preparedItemId.HasValue
            ? Models.Enums.Inventory.BtpIdentityState.Canonical
            : recipeId.HasValue
                ? Models.Enums.Inventory.BtpIdentityState.Legacy
                : null,
        QuantitySemanticsStatus = preparedItemId.HasValue
            ? InventoryQuantitySemanticsStatus.BaseUnitConfirmed
            : recipeId.HasValue
                ? InventoryQuantitySemanticsStatus.Unknown
                : null,
        QuantitySemanticsEvidenceType = preparedItemId.HasValue
            ? Models.Enums.Inventory.QuantitySemanticsEvidenceType.SystemCanonicalCreation
            : null,
        QuantitySemanticsEvidenceReference = preparedItemId.HasValue ? "issue-196-sql-test" : null,
        QuantitySemanticsReviewedAt = preparedItemId.HasValue ? DateTime.UtcNow : null,
        QuantitySemanticsReviewedByAccountId = preparedItemId.HasValue ? 1 : null,
        AvailableQty = 10m,
        ReservedQty = 0m,
        LastUpdated = DateTime.UtcNow
    };

    private static InventoryTransaction Transaction(
        int inventoryId,
        InventoryTransactionTypeEnum type,
        DateTime createdAt) => new()
    {
        StoreInventoryId = inventoryId,
        Type = type,
        StockStatus = InventoryStockStatus.NORMAL,
        Quantity = 1m,
        BeforeQty = 0m,
        AfterQty = 1m,
        UnitCost = null,
        TotalCost = null,
        CreatedAt = createdAt
    };

    private sealed record SeedResult(int StoreId, int OtherStoreId, int RecipeId);
}
