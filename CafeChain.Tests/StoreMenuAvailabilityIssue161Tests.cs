using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Profitability;
using CafeChain.Application.Services.Admin.Profitability;
using CafeChain.Application.Services.Admin.StoreMenu;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeChain.Tests;

public sealed class StoreMenuAvailabilityIssue161Tests : IntegrationTestBase
{
    private const int StoreId = 16101;
    private const int UnitId = 16102;
    private const int MainIngredientId = 16103;
    private const int ToppingIngredientId = 16104;
    private const int DrinkId = 16105;
    private const int MediumSizeId = 16106;
    private const int LargeSizeId = 16107;
    private const int MediumDrinkSizeId = 16108;
    private const int LargeDrinkSizeId = 16109;
    private const int MediumRecipeId = 16110;
    private const int LargeRecipeId = 16111;
    private const int MainInventoryId = 16114;
    private const int ToppingId = 16120;
    private const int ToppingRecipeId = 16121;
    private const int OwnerStaffId = 16130;

    [Fact]
    public async Task Availability_IsPerSku_AndStockTransitionsDoNotMutateConfiguration()
    {
        await SeedAsync();
        await using var context = CreateDbContext();
        var evaluator = CreateEvaluator(context);

        var medium = await evaluator.EvaluateAsync(StoreId, MediumDrinkSizeId, DateTime.UtcNow);
        var large = await evaluator.EvaluateAsync(StoreId, LargeDrinkSizeId, DateTime.UtcNow);

        Assert.Equal(StoreMenuAvailabilityStatuses.Available, medium.OperationalStatus);
        Assert.True(medium.IsSellable);
        Assert.Equal(StoreMenuAvailabilityStatuses.OutOfStock, large.OperationalStatus);
        Assert.False(large.IsSellable);

        var inventory = await context.StoreInventories.SingleAsync(x => x.StoreInventoryId == MainInventoryId);
        inventory.MinStockLevel = 1m;
        await context.SaveChangesAsync();
        var low = await evaluator.EvaluateAsync(StoreId, MediumDrinkSizeId, DateTime.UtcNow);
        Assert.Equal(StoreMenuAvailabilityStatuses.LowStock, low.OperationalStatus);
        Assert.True(low.IsSellable);

        inventory.AvailableQty = 1m;
        await context.SaveChangesAsync();
        var soldOut = await evaluator.EvaluateAsync(StoreId, MediumDrinkSizeId, DateTime.UtcNow);
        Assert.Equal(StoreMenuAvailabilityStatuses.OutOfStock, soldOut.OperationalStatus);

        inventory.AvailableQty = 10m;
        await context.SaveChangesAsync();
        var recovered = await evaluator.EvaluateAsync(StoreId, MediumDrinkSizeId, DateTime.UtcNow);
        Assert.Equal(StoreMenuAvailabilityStatuses.Available, recovered.OperationalStatus);
        Assert.All(await context.StoreMenuItems.Where(x => x.StoreId == StoreId).ToListAsync(), x => Assert.True(x.IsEnabled));
    }

    [Fact]
    public async Task RequiredDefaultTopping_UnavailableBlocksOnlyAffectedSku()
    {
        await SeedAsync(includeRequiredTopping: true);
        await using var context = CreateDbContext();
        var evaluator = CreateEvaluator(context);

        var blocked = await evaluator.EvaluateAsync(StoreId, MediumDrinkSizeId, DateTime.UtcNow);
        var unaffected = await evaluator.EvaluateAsync(StoreId, LargeDrinkSizeId, DateTime.UtcNow);

        Assert.Equal(StoreMenuAvailabilityStatuses.ToppingUnavailable, blocked.OperationalStatus);
        Assert.False(blocked.IsSellable);
        Assert.NotEqual(StoreMenuAvailabilityStatuses.ToppingUnavailable, unaffected.OperationalStatus);

        var toppingInventory = await context.StoreInventories.SingleAsync(x => x.IngredientId == ToppingIngredientId);
        toppingInventory.AvailableQty = 5m;
        await context.SaveChangesAsync();

        var recovered = await evaluator.EvaluateAsync(StoreId, MediumDrinkSizeId, DateTime.UtcNow);
        Assert.Equal(StoreMenuAvailabilityStatuses.Available, recovered.OperationalStatus);
        Assert.True(recovered.IsSellable);
    }

    [Fact]
    public async Task RequiredPolicy_MustAlsoBeDefaultSelected()
    {
        await SeedAsync();
        await using var context = CreateDbContext();
        var service = new DrinkSizeToppingPolicyService(context);

        var result = await service.UpsertAsync(new UpsertDrinkSizeToppingPolicyRequest
        {
            DrinkSizeId = MediumDrinkSizeId,
            ToppingId = ToppingId,
            IsDefaultSelected = false,
            IsRequired = true,
            PriceTreatment = ToppingPriceTreatments.IncludedInBasePrice,
            CostTreatment = ToppingCostTreatments.AddToppingRecipeCost,
            QuantityPerDrink = 1m
        }, OwnerStaffId);

        Assert.False(result.IsSuccess);
        Assert.Equal("Topping bắt buộc phải được chọn mặc định.", result.Message);
    }

    private async Task SeedAsync(bool includeRequiredTopping = false)
    {
        await using var context = CreateDbContext();
        context.Stores.Add(new Store
        {
            StoreId = StoreId, Name = "Store Menu D", Address = "Test", Phone = "161", Active = true,
            CreatedAt = DateTime.UtcNow
        });
        context.Units.Add(new Unit { UnitId = UnitId, UnitCode = "SM161", Name = "unit", Active = true });
        context.Ingredients.AddRange(
            new Ingredient { IngredientId = MainIngredientId, Code = "SM161_MAIN", Name = "Main", BaseUnitId = UnitId, Active = true },
            new Ingredient { IngredientId = ToppingIngredientId, Code = "SM161_TOP", Name = "Top", BaseUnitId = UnitId, Active = true });
        context.Sizes.AddRange(
            new Size { SizeId = MediumSizeId, SizeCode = "SM161_M", Name = "Store Menu 161 M", Description = "M", Active = true },
            new Size { SizeId = LargeSizeId, SizeCode = "SM161_L", Name = "Store Menu 161 L", Description = "L", Active = true });
        context.Drinks.Add(new Drink
        {
            DrinkId = DrinkId, DrinkCode = "SM161_DRINK", Name = "Store Menu Drink", Description = "Test",
            ProductTypeId = 1, Active = true, CreatedAt = DateTime.UtcNow
        });
        context.DrinkSizes.AddRange(
            new DrinkSize { DrinkSizeId = MediumDrinkSizeId, DrinkId = DrinkId, SizeId = MediumSizeId, Price = 20_000m, Active = true },
            new DrinkSize { DrinkSizeId = LargeDrinkSizeId, DrinkId = DrinkId, SizeId = LargeSizeId, Price = 25_000m, Active = true });
        context.Recipes.AddRange(
            NewRecipe(MediumRecipeId, MediumSizeId),
            NewRecipe(LargeRecipeId, LargeSizeId));
        context.RecipeDetails.AddRange(
            new RecipeDetail { RecipeDetailId = 16112, RecipeId = MediumRecipeId, IngredientId = MainIngredientId, Quantity = 2m, UnitId = UnitId },
            new RecipeDetail { RecipeDetailId = 16113, RecipeId = LargeRecipeId, IngredientId = MainIngredientId, Quantity = 5m, UnitId = UnitId });
        context.StoreInventories.Add(new StoreInventory
        {
            StoreInventoryId = MainInventoryId, StoreId = StoreId, IngredientId = MainIngredientId,
            AvailableQty = 3m, ReservedQty = 0m, LastUpdated = DateTime.UtcNow
        });
        var published = DateTime.UtcNow.AddMinutes(-1);
        context.StoreMenuItems.AddRange(
            new StoreMenuItem { StoreMenuItemId = 16115, StoreId = StoreId, DrinkSizeId = MediumDrinkSizeId, IsEnabled = true, PublishedAtUtc = published, CreatedAtUtc = published, UpdatedAtUtc = published },
            new StoreMenuItem { StoreMenuItemId = 16116, StoreId = StoreId, DrinkSizeId = LargeDrinkSizeId, IsEnabled = true, PublishedAtUtc = published, CreatedAtUtc = published, UpdatedAtUtc = published });
        context.Toppings.Add(new Topping { ToppingId = ToppingId, ToppingCode = "SM161_TOP", Name = "Required top", Price = 0m, Active = true });
        context.DrinkToppings.Add(new DrinkTopping { DrinkToppingId = 16122, DrinkId = DrinkId, ToppingId = ToppingId, Active = true });

        if (includeRequiredTopping)
        {
            context.StoreToppings.Add(new StoreTopping { StoreToppingId = 16123, StoreId = StoreId, ToppingId = ToppingId, Active = true });
            context.Recipes.Add(new Recipe
            {
                RecipeId = ToppingRecipeId, RecipeCode = "SM161_TOP_R", Name = "Topping recipe", ToppingId = ToppingId,
                Active = true, Status = "Active", EffectiveDate = DateTime.UtcNow.AddDays(-1)
            });
            context.RecipeDetails.Add(new RecipeDetail
            {
                RecipeDetailId = 16124, RecipeId = ToppingRecipeId, IngredientId = ToppingIngredientId,
                Quantity = 1m, UnitId = UnitId
            });
            context.StoreInventories.Add(new StoreInventory
            {
                StoreInventoryId = 16125, StoreId = StoreId, IngredientId = ToppingIngredientId,
                AvailableQty = 0m, ReservedQty = 0m, LastUpdated = DateTime.UtcNow
            });
            context.DrinkSizeToppingPolicies.Add(new DrinkSizeToppingPolicy
            {
                DrinkSizeToppingPolicyId = 16126, DrinkSizeId = MediumDrinkSizeId, ToppingId = ToppingId,
                IsDefaultSelected = true, IsRequired = true,
                PriceTreatment = ToppingPriceTreatments.IncludedInBasePrice,
                CostTreatment = ToppingCostTreatments.AddToppingRecipeCost,
                QuantityPerDrink = 1m, IsActive = true, CreatedByStaffId = OwnerStaffId,
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            });
        }

        var ownerRole = await context.Roles.SingleAsync(x => x.Name == RoleConstants.BusinessOwner);
        context.Accounts.Add(new Account
        {
            AccountId = OwnerStaffId, Email = "sm161-owner@test.local", PasswordHash = "x", Active = true,
            CreatedAt = DateTime.UtcNow
        });
        context.Staffs.Add(new Staff
        {
            StaffId = OwnerStaffId, AccountId = OwnerStaffId, StoreId = StoreId, FullName = "Store Menu Owner",
            Active = true, CreatedAt = DateTime.UtcNow, EmployeeStatus = 2});
        context.AccountRoles.Add(new AccountRole { AccountId = OwnerStaffId, RoleId = ownerRole.RoleId });
        await context.SaveChangesAsync();
    }

    private static Recipe NewRecipe(int recipeId, int sizeId) => new()
    {
        RecipeId = recipeId,
        RecipeCode = $"SM161_R_{recipeId}",
        Name = $"Recipe {recipeId}",
        DrinkId = DrinkId,
        SizeId = sizeId,
        Active = true,
        Status = "Active",
        EffectiveDate = DateTime.UtcNow.AddDays(-1)
    };

    private static StoreMenuAvailabilityEvaluator CreateEvaluator(AppDbContext context)
    {
        var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
        var conversion = new UnitConversionService(context, NullLogger<UnitConversionService>.Instance, physical);
        var resolver = new DrinkSizeRecipeResolver(context, conversion, physical);
        return new StoreMenuAvailabilityEvaluator(context, resolver, conversion, physical);
    }
}
