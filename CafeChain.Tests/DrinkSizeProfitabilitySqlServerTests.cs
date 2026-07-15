using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Profitability;
using CafeChain.Application.Services.Admin.Profitability;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.Security;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class DrinkSizeProfitabilitySqlServerTests : IAsyncLifetime
{
    private const string Database = "CafeChain_DrinkProfitabilityTests";
    private int _storeId;
    private int _drinkId;
    private int _sizeId;
    private int _drinkSizeId;
    private int _unitId;
    private int _ingredientId;
    private int _recipeId;
    private int _toppingId;
    private int _ownerStaffId;

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
            await SeedAsync(context);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"SQL Server integration environment unavailable. Database={Database}. {ex.Message}", ex);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SqlServer_OneEffectiveRecipePerDrinkSize()
    {
        await using var context = CreateContext();
        context.Recipes.Add(new Recipe
        {
            RecipeCode = "PF-SQL-DUP", Name = "Duplicate", DrinkId = _drinkId, SizeId = _sizeId,
            Active = true, Status = "Active", EffectiveDate = DateTime.UtcNow
        });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("UX_Recipes_OneActive_Drink_Size", exception.InnerException?.Message ?? exception.Message);
    }

    [Fact]
    public async Task SqlServer_OneActiveToppingPolicyPerDrinkSizeTopping()
    {
        await using var context = CreateContext();
        context.DrinkSizeToppingPolicies.AddRange(NewPolicy(1), NewPolicy(2));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("UX_DrinkSizeToppingPolicies_Active", exception.InnerException?.Message ?? exception.Message);
    }

    [Fact]
    public async Task SqlServer_CostPreview_DoesNotConsumeLayers()
    {
        await using var context = CreateContext();
        var service = CreateProfitability(context);
        var before = await context.InventoryCostLayers.Where(x => x.StoreId == _storeId).Select(x => x.RemainingQuantity).SingleAsync();

        var first = await service.PreviewAsync(_storeId, _drinkId, DateTime.UtcNow, _ownerStaffId);
        var second = await service.PreviewAsync(_storeId, _drinkId, DateTime.UtcNow, _ownerStaffId);

        Assert.True(first.IsSuccess && second.IsSuccess);
        Assert.Equal(before, await context.InventoryCostLayers.Where(x => x.StoreId == _storeId).Select(x => x.RemainingQuantity).SingleAsync());
    }

    [Fact]
    public async Task SqlServer_ConcurrentPriceUpdate_AllowsOneWinner()
    {
        string rowVersion;
        await using (var read = CreateContext())
            rowVersion = Convert.ToBase64String((await read.DrinkSizes.AsNoTracking().SingleAsync(x => x.DrinkSizeId == _drinkSizeId)).RowVersion);

        var tasks = new[] { 31_000m, 32_000m }.Select(async price =>
        {
            await using var context = CreateContext();
            return await CreatePricing(context).UpdatePriceAsync(new UpdateDrinkSizePriceRequest
            {
                DrinkSizeId = _drinkSizeId, NewSellingPrice = price, ExpectedRowVersion = rowVersion, Reason = "Concurrent SQL test"
            }, _storeId, _ownerStaffId);
        });

        var results = await Task.WhenAll(tasks);
        Assert.Single(results.Where(x => x.IsSuccess));
        Assert.Single(results.Where(x => !x.IsSuccess && x.ErrorCode == "PRICE_CHANGED_BY_ANOTHER_USER"));
        await using var verify = CreateContext();
        Assert.Single(await verify.DrinkSizePriceAudits.ToListAsync());
    }

    [Fact]
    public async Task SqlServer_PriceAuditAndUpdate_AreAtomic()
    {
        await using var setup = CreateContext();
        await setup.Database.ExecuteSqlRawAsync("""
            CREATE OR ALTER TRIGGER TR_PF_RejectPriceAudit ON DrinkSizePriceAudits
            INSTEAD OF INSERT AS
            THROW 51000, 'PF audit failure', 1;
            """);
        var rowVersion = Convert.ToBase64String((await setup.DrinkSizes.AsNoTracking().SingleAsync(x => x.DrinkSizeId == _drinkSizeId)).RowVersion);

        await Assert.ThrowsAnyAsync<Exception>(() => CreatePricing(setup).UpdatePriceAsync(new UpdateDrinkSizePriceRequest
        {
            DrinkSizeId = _drinkSizeId, NewSellingPrice = 33_000, ExpectedRowVersion = rowVersion, Reason = "Atomic test"
        }, _storeId, _ownerStaffId));

        await using var verify = CreateContext();
        Assert.Equal(30_000m, (await verify.DrinkSizes.AsNoTracking().SingleAsync(x => x.DrinkSizeId == _drinkSizeId)).Price);
        Assert.Empty(await verify.DrinkSizePriceAudits.ToListAsync());
    }

    [Fact]
    public async Task SqlServer_CatalogVersion_IncrementsOnce()
    {
        await using var context = CreateContext();
        var rowVersion = Convert.ToBase64String((await context.DrinkSizes.AsNoTracking().SingleAsync(x => x.DrinkSizeId == _drinkSizeId)).RowVersion);

        var result = await CreatePricing(context).UpdatePriceAsync(new UpdateDrinkSizePriceRequest
        {
            DrinkSizeId = _drinkSizeId, NewSellingPrice = 31_500, ExpectedRowVersion = rowVersion, Reason = "Catalog test"
        }, _storeId, _ownerStaffId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(1, result.Data.CatalogVersion);
        Assert.Equal(1, (await context.PosCatalogStates.AsNoTracking().SingleAsync()).Version);
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(ConnectionString)
        .Options);

    private async Task SeedAsync(AppDbContext context)
    {
        var store = new Store { Name = "PF SQL Store", Address = "Test", Phone = "0900000000", Active = true, CreatedAt = DateTime.UtcNow };
        var unit = new Unit { UnitCode = "pf-sql-unit", Name = "PF SQL unit", Active = true };
        var size = new Size { SizeCode = "PF-SQL-M", Name = "PF SQL Size", Description = "Test", Active = true };
        var drink = new Drink { DrinkCode = "PF-SQL-DRINK", Name = "PF SQL Drink", Description = "Test", ProductTypeId = 1, Active = true, CreatedAt = DateTime.UtcNow };
        var topping = new Topping { ToppingCode = "PF-SQL-TOP", Name = "PF SQL Topping", Price = 5_000, Active = true };
        var account = new Account { Email = "pf-sql-owner@test.local", PasswordHash = "x", Active = true, CreatedAt = DateTime.UtcNow };
        context.AddRange(store, unit, size, drink, topping, account);
        await context.SaveChangesAsync();

        _storeId = store.StoreId;
        _unitId = unit.UnitId;
        _sizeId = size.SizeId;
        _drinkId = drink.DrinkId;
        _toppingId = topping.ToppingId;

        var ingredient = new Ingredient { Code = "PF-SQL-ING", Name = "PF SQL Ingredient", BaseUnitId = _unitId, Active = true };
        var drinkSize = new DrinkSize { DrinkId = _drinkId, SizeId = _sizeId, Price = 30_000, Active = true, UpdatedAtUtc = DateTime.UtcNow };
        var role = await context.Roles.FirstAsync(x => x.Name == RoleConstants.BusinessOwner);
        var staff = new Staff { AccountId = account.AccountId, FullName = "PF SQL Owner", StoreId = _storeId, Active = true, CreatedAt = DateTime.UtcNow, EmployeeStatus = 2, SalaryType = 1 };
        context.AddRange(ingredient, drinkSize, staff);
        context.AccountRoles.Add(new AccountRole { AccountId = account.AccountId, RoleId = role.RoleId });
        await context.SaveChangesAsync();

        _ingredientId = ingredient.IngredientId;
        _drinkSizeId = drinkSize.DrinkSizeId;
        _ownerStaffId = staff.StaffId;

        var recipe = new Recipe { RecipeCode = "PF-SQL-RCP", Name = "PF SQL Recipe", DrinkId = _drinkId, SizeId = _sizeId, Active = true, Status = "Active", EffectiveDate = DateTime.UtcNow.AddDays(-1) };
        context.Recipes.Add(recipe);
        await context.SaveChangesAsync();
        _recipeId = recipe.RecipeId;

        context.RecipeDetails.Add(new RecipeDetail { RecipeId = _recipeId, IngredientId = _ingredientId, Quantity = 1, UnitId = _unitId });
        context.InventoryCostLayers.Add(new InventoryCostLayer { IngredientId = _ingredientId, StoreId = _storeId, Quantity = 100, RemainingQuantity = 100, UnitCost = 10, CreatedAt = DateTime.UtcNow.AddDays(-1) });
        context.DrinkToppings.Add(new DrinkTopping { DrinkId = _drinkId, ToppingId = _toppingId, Active = true });
        await context.SaveChangesAsync();
    }

    private DrinkSizeToppingPolicy NewPolicy(int suffix) => new()
    {
        DrinkSizeId = _drinkSizeId, ToppingId = _toppingId, IsDefaultSelected = true,
        PriceTreatment = ToppingPriceTreatments.IncludedInBasePrice,
        CostTreatment = ToppingCostTreatments.IncludedInDrinkRecipe,
        QuantityPerDrink = 1, IsActive = true, CreatedByStaffId = _ownerStaffId,
        CreatedAtUtc = DateTime.UtcNow.AddSeconds(suffix), UpdatedAtUtc = DateTime.UtcNow.AddSeconds(suffix)
    };

    private static DrinkSizeProfitabilityQueryService CreateProfitability(AppDbContext context)
    {
        var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
        var conversion = new UnitConversionService(context, NullLogger<UnitConversionService>.Instance, physical);
        var normalizer = new RecipeOutputNormalizer(context, physical);
        var estimated = new EstimatedBomCostService(context, conversion, physical, normalizer, NullLogger<EstimatedBomCostService>.Instance);
        return new DrinkSizeProfitabilityQueryService(context, new DrinkSizeRecipeResolver(context, conversion, physical), conversion, physical, estimated, new ScopeAuthorizationService(context));
    }

    private static DrinkSizePricingService CreatePricing(AppDbContext context) => new(context, CreateProfitability(context));
}
