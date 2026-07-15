using CafeChain.Application.Constants;
using CafeChain.Application.Services.Admin.Profitability;
using CafeChain.Application.Services.Admin.StoreMenu;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.POS;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeChain.Tests;

public sealed class StoreCatalogSyncIssue163Tests : IntegrationTestBase
{
    private const int StoreA = 16301;
    private const int StoreB = 16302;
    private const int CategoryId = 16303;
    private const int DrinkId = 16304;
    private const int SizeId = 16305;
    private const int DrinkSizeId = 16306;
    private const int UnitId = 16307;
    private const int IngredientId = 16308;
    private const int RecipeId = 16309;

    [Fact]
    public async Task StoreCatalogVersions_AreIsolated_AndAvailabilityHashOnlyChangesAffectedStore()
    {
        await SeedAsync();
        await using var context = CreateDbContext();
        var service = CreateService(context);

        var firstA = await service.BuildAsync(StoreA, DateTime.UtcNow);
        var firstB = await service.BuildAsync(StoreB, DateTime.UtcNow);
        var sameA = await service.BuildAsync(StoreA, DateTime.UtcNow);

        Assert.Equal(StoreA, firstA.StoreId);
        Assert.Equal(StoreB, firstB.StoreId);
        Assert.Equal(1, firstA.Version);
        Assert.Equal(1, firstB.Version);
        Assert.Equal(firstA.Version, sameA.Version);
        Assert.Equal(20_000m, firstA.MenuItems.Single().Sizes.Single().Price);
        Assert.Equal(30_000m, firstB.MenuItems.Single().Sizes.Single().Price);
        Assert.All(firstA.MenuItems.Single().Sizes, x => Assert.Equal(16310, x.StoreMenuItemId));
        Assert.All(firstB.MenuItems.Single().Sizes, x => Assert.Equal(16311, x.StoreMenuItemId));

        var inventoryA = await context.StoreInventories.SingleAsync(x => x.StoreId == StoreA && x.IngredientId == IngredientId);
        inventoryA.AvailableQty = 0m;
        await context.SaveChangesAsync();

        var changedA = await service.BuildAsync(StoreA, DateTime.UtcNow);
        Assert.Equal(2, changedA.Version);
        Assert.False(changedA.MenuItems.Single().Sizes.Single().IsAvailable);
        Assert.Equal(1, (await context.PosCatalogStates.AsNoTracking().SingleAsync(x => x.StoreId == StoreB)).Version);
    }

    [Fact]
    public void FrontendCatalogCache_IsStoreScopedAndReplacedAtomically()
    {
        var root = FindRepoRoot();
        var dbSource = File.ReadAllText(Path.Combine(root, "CafeChain.Frontend", "src", "db", "CafeChainPOSDB.ts"));
        var syncSource = File.ReadAllText(Path.Combine(root, "CafeChain.Frontend", "src", "services", "OfflineSyncService.ts"));
        var hookSource = File.ReadAllText(Path.Combine(root, "CafeChain.Frontend", "src", "hooks", "usePOSData.ts"));

        Assert.Contains("[storeId+id]", dbSource);
        Assert.Contains("catalogStates", dbSource);
        Assert.Contains("db.transaction('rw', db.categories, db.menuItems, db.catalogStates", syncSource);
        Assert.Contains("/api/v1/pos/catalog", syncSource);
        Assert.Contains("snapshot.storeId !== storeId", syncSource);
        Assert.Contains("pos-session-changed", hookSource);
        Assert.Contains("where('storeId').equals(storeId)", hookSource);
        Assert.DoesNotContain("pos_catalog_version'", syncSource);
    }

    private async Task SeedAsync()
    {
        await using var context = CreateDbContext();
        context.Stores.AddRange(
            new Store { StoreId = StoreA, Name = "Catalog A", Address = "A", Phone = "16301", Active = true, CreatedAt = DateTime.UtcNow },
            new Store { StoreId = StoreB, Name = "Catalog B", Address = "B", Phone = "16302", Active = true, CreatedAt = DateTime.UtcNow });
        context.DrinkCategories.Add(new DrinkCategory { CategoryId = CategoryId, CategoryCode = "SM163", Name = "Store Menu 163", Icon = "C", Active = true });
        context.Units.Add(new Unit { UnitId = UnitId, UnitCode = "SM163", Name = "Store Menu Unit 163", Active = true });
        context.Ingredients.Add(new Ingredient { IngredientId = IngredientId, Code = "SM163", Name = "Store Menu Ingredient 163", BaseUnitId = UnitId, Active = true });
        context.Sizes.Add(new Size { SizeId = SizeId, SizeCode = "SM163", Name = "Store Menu Size 163", Description = "Test", Active = true });
        context.Drinks.Add(new Drink
        {
            DrinkId = DrinkId, DrinkCode = "SM163", Name = "Store Menu Drink 163", Description = "Test",
            ProductTypeId = 1, CategoryId = CategoryId, Active = true, CreatedAt = DateTime.UtcNow
        });
        context.DrinkSizes.Add(new DrinkSize { DrinkSizeId = DrinkSizeId, DrinkId = DrinkId, SizeId = SizeId, Price = 20_000m, Active = true });
        context.Recipes.Add(new Recipe
        {
            RecipeId = RecipeId, RecipeCode = "SM163_R", Name = "Store Menu Recipe 163",
            DrinkId = DrinkId, SizeId = SizeId, Active = true, Status = "Active", EffectiveDate = DateTime.UtcNow.AddDays(-1)
        });
        context.RecipeDetails.Add(new RecipeDetail
        {
            RecipeDetailId = 16312, RecipeId = RecipeId, IngredientId = IngredientId, Quantity = 1m, UnitId = UnitId
        });
        context.StoreInventories.AddRange(
            new StoreInventory { StoreInventoryId = 16313, StoreId = StoreA, IngredientId = IngredientId, AvailableQty = 5m, ReservedQty = 0m, LastUpdated = DateTime.UtcNow },
            new StoreInventory { StoreInventoryId = 16314, StoreId = StoreB, IngredientId = IngredientId, AvailableQty = 5m, ReservedQty = 0m, LastUpdated = DateTime.UtcNow });
        var published = DateTime.UtcNow.AddDays(-1);
        context.StoreMenuItems.AddRange(
            new StoreMenuItem { StoreMenuItemId = 16310, StoreId = StoreA, DrinkSizeId = DrinkSizeId, IsEnabled = true, PublishedAtUtc = published, CreatedAtUtc = published, UpdatedAtUtc = published },
            new StoreMenuItem { StoreMenuItemId = 16311, StoreId = StoreB, DrinkSizeId = DrinkSizeId, IsEnabled = true, PriceOverride = 30_000m, PublishedAtUtc = published, CreatedAtUtc = published, UpdatedAtUtc = published });
        await context.SaveChangesAsync();
    }

    private static POSCatalogSnapshotService CreateService(AppDbContext context)
    {
        var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
        var conversion = new UnitConversionService(context, NullLogger<UnitConversionService>.Instance, physical);
        var resolver = new DrinkSizeRecipeResolver(context, conversion, physical);
        var availability = new StoreMenuAvailabilityEvaluator(context, resolver, conversion, physical);
        return new POSCatalogSnapshotService(context, availability);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "CafeChain.Frontend"))
                && Directory.Exists(Path.Combine(current.FullName, "CafeChain")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
