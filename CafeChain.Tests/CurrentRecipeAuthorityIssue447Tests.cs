using CafeChain.Application.DTOs.Admin.Recipes;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.Systems;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.ViewModels.Admin.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeChain.Tests;

public sealed class CurrentRecipeAuthorityIssue447Tests : IntegrationTestBase
{
    private static readonly DateTime BusinessInstantUtc =
        new(2026, 8, 14, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CurrentResolver_RequiresExactMenuItemSizeTarget()
    {
        await using var context = CreateDbContext();
        var exact = await context.Recipes.SingleAsync(x => x.RecipeId == 1);
        exact.EffectiveDate = BusinessInstantUtc.AddDays(-1);
        context.Recipes.Add(new Recipe
        {
            RecipeCode = "LEGACY-SIZELESS",
            Name = "Công thức không có cỡ",
            DrinkId = exact.DrinkId,
            SizeId = null,
            Active = true,
            Status = "Active",
            EffectiveDate = BusinessInstantUtc.AddDays(-2)
        });
        await context.SaveChangesAsync();

        var resolver = new CurrentRecipeResolver(context);
        var found = await resolver.ResolveAsync(
            new RecipeTarget.MenuItemSize(exact.DrinkId!.Value, exact.SizeId!.Value),
            BusinessInstantUtc);

        Assert.Equal(CurrentRecipeResolutionStatus.Found, found.Status);
        Assert.Equal(exact.RecipeId, found.Recipe?.RecipeId);

        exact.Active = false;
        exact.Status = "Archived";
        await context.SaveChangesAsync();

        var missing = await resolver.ResolveAsync(
            new RecipeTarget.MenuItemSize(exact.DrinkId.Value, exact.SizeId.Value),
            BusinessInstantUtc);

        Assert.Equal(CurrentRecipeResolutionStatus.Missing, missing.Status);
        Assert.Null(missing.Recipe);
    }

    [Fact]
    public async Task CurrentResolver_ResolvesToppingAndPreparedItemTargetsExactly()
    {
        await using var context = CreateDbContext();
        var toppingRecipe = await context.Recipes.SingleAsync(recipe => recipe.RecipeId == 5);
        toppingRecipe.EffectiveDate = BusinessInstantUtc.AddDays(-1);

        var preparedItem = new CafeChain.Models.Inventories.PreparedItems.PreparedItem
        {
            Code = "BTP-ISSUE-447",
            Name = "Bán thành phẩm kiểm thử",
            BaseUnitId = 1,
            Active = true
        };
        context.PreparedItems.Add(preparedItem);
        await context.SaveChangesAsync();
        var preparedRecipe = new Recipe
        {
            RecipeCode = "RCP-BTP-ISSUE-447",
            Name = preparedItem.Name,
            Active = true,
            Status = "Active",
            EffectiveDate = BusinessInstantUtc.AddDays(-1),
            PreparedItemId = preparedItem.PreparedItemId,
            OutputQuantity = 1000m,
            OutputUnitId = 1
        };
        context.Recipes.Add(preparedRecipe);
        await context.SaveChangesAsync();

        var resolver = new CurrentRecipeResolver(context);
        var topping = await resolver.ResolveAsync(
            new RecipeTarget.Topping(toppingRecipe.ToppingId!.Value),
            BusinessInstantUtc);
        var prepared = await resolver.ResolveAsync(
            new RecipeTarget.PreparedItem(preparedItem.PreparedItemId),
            BusinessInstantUtc);

        Assert.Equal(toppingRecipe.RecipeId, topping.Recipe?.RecipeId);
        Assert.Equal(preparedRecipe.RecipeId, prepared.Recipe?.RecipeId);
    }

    [Fact]
    public async Task LegacyAmbiguousCurrentRecipe_FailsClosedWithoutRewrite()
    {
        await using var context = CreateDbContext();
        var existing = await context.Recipes.SingleAsync(recipe => recipe.RecipeId == 5);
        existing.EffectiveDate = BusinessInstantUtc.AddDays(-2);
        context.Recipes.Add(new Recipe
        {
            RecipeCode = "LEGACY-TOPPING-DUPLICATE",
            Name = "Bằng chứng topping bị trùng",
            ToppingId = existing.ToppingId,
            Active = true,
            Status = "Active",
            EffectiveDate = BusinessInstantUtc.AddDays(-1)
        });
        await context.SaveChangesAsync();
        var before = await context.Recipes.CountAsync(recipe =>
            recipe.ToppingId == existing.ToppingId
            && recipe.Active
            && recipe.Status == "Active");

        var resolver = new CurrentRecipeResolver(context);
        var first = await resolver.ResolveAsync(
            new RecipeTarget.Topping(existing.ToppingId!.Value),
            BusinessInstantUtc);
        var second = await resolver.ResolveAsync(
            new RecipeTarget.Topping(existing.ToppingId.Value),
            BusinessInstantUtc);

        Assert.Equal(CurrentRecipeResolutionStatus.Ambiguous, first.Status);
        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.ReasonCode, second.ReasonCode);
        Assert.Null(first.Recipe);
        Assert.Equal(before, await context.Recipes.CountAsync(recipe =>
            recipe.ToppingId == existing.ToppingId
            && recipe.Active
            && recipe.Status == "Active"));
    }

    [Fact]
    public async Task PublishingNewVersion_AppliesImmediately()
    {
        await using var context = CreateDbContext();
        var source = await LoadSourceAsync(context);
        var model = await BuildVersionModelAsync(context, source);

        var result = await CreateService(context).UpdateRecipeAsync(source.RecipeId, model);

        Assert.True(result.IsSuccess, result.Message);
        var current = await new CurrentRecipeResolver(context).ResolveAsync(
            new RecipeTarget.MenuItemSize(source.DrinkId!.Value, source.SizeId!.Value),
            BusinessInstantUtc);
        Assert.Equal(CurrentRecipeResolutionStatus.Found, current.Status);
        Assert.NotEqual(source.RecipeId, current.Recipe?.RecipeId);
        Assert.Equal(BusinessInstantUtc, current.Recipe?.EffectiveDate);
    }

    [Fact]
    public async Task PublishingNewVersion_ArchivesPreviousVersionAtomically()
    {
        await using var context = CreateDbContext();
        var source = await LoadSourceAsync(context);
        var model = await BuildVersionModelAsync(context, source);

        var result = await CreateService(context).UpdateRecipeAsync(source.RecipeId, model);

        Assert.True(result.IsSuccess, result.Message);
        context.ChangeTracker.Clear();
        var versions = await context.Recipes
            .Where(recipe => recipe.DrinkId == source.DrinkId && recipe.SizeId == source.SizeId)
            .OrderBy(recipe => recipe.RecipeId)
            .ToListAsync();
        Assert.Single(versions.Where(recipe => recipe.Active && recipe.Status == "Active"));
        var archived = Assert.Single(versions.Where(recipe => recipe.RecipeId == source.RecipeId));
        Assert.False(archived.Active);
        Assert.Equal("Archived", archived.Status);
        Assert.Contains(versions, recipe =>
            recipe.ParentVersionId == source.RecipeId
            && recipe.Active
            && recipe.Status == "Active");
    }

    [Fact]
    public async Task FutureEffectiveDate_IsRejectedInNewFlow()
    {
        await using var context = CreateDbContext();
        var source = await LoadSourceAsync(context);
        var model = await BuildVersionModelAsync(context, source);
        model.EffectiveDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Unspecified);

        var result = await CreateService(context).UpdateRecipeAsync(source.RecipeId, model);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            CafeChain.Application.Constants.BomRecipeErrorCodes.FutureEffectiveDateNotSupported,
            result.ErrorCode);
        Assert.Contains("không hỗ trợ ngày áp dụng trong tương lai", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(await context.Recipes.AnyAsync(recipe =>
            recipe.RecipeId == source.RecipeId
            && recipe.Active
            && recipe.Status == "Active"));
    }

    [Fact]
    public async Task PublishConflict_IsBusinessReadable()
    {
        await using var context = CreateDbContext();
        var source = await LoadSourceAsync(context);
        var model = await BuildVersionModelAsync(context, source);
        source.Active = false;
        source.Status = "Archived";
        await context.SaveChangesAsync();

        var result = await CreateService(context).UpdateRecipeAsync(source.RecipeId, model);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            CafeChain.Application.Constants.BomRecipeErrorCodes.PublishConflict,
            result.ErrorCode);
        Assert.Contains("tải lại", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DbUpdateException", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Archived", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Recipe> LoadSourceAsync(AppDbContext context)
    {
        return await context.Recipes
            .Include(recipe => recipe.RecipeDetails)
            .SingleAsync(recipe => recipe.RecipeId == 1);
    }

    private static async Task<RecipeCreateVM> BuildVersionModelAsync(
        AppDbContext context,
        Recipe source)
    {
        var details = source.RecipeDetails.Select(detail => new RecipeDetailVM
        {
            ItemCode = detail.IngredientId.HasValue
                ? $"ING_{detail.IngredientId.Value}"
                : $"REC_{detail.ChildRecipeId!.Value}",
            Quantity = detail.Quantity,
            UnitId = detail.UnitId,
            YieldPercentage = 100m
        }).ToList();

        if (details.Count == 0)
        {
            var unitId = await context.Ingredients
                .Where(ingredient => ingredient.IngredientId == 1)
                .Select(ingredient => ingredient.BaseUnitId)
                .SingleAsync();
            details.Add(new RecipeDetailVM
            {
                ItemCode = "ING_1",
                Quantity = 10m,
                UnitId = unitId,
                YieldPercentage = 100m
            });
        }

        return new RecipeCreateVM
        {
            RecipeType = "POS",
            DrinkId = source.DrinkId,
            SizeId = source.SizeId,
            Active = true,
            EffectiveDate = new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Unspecified),
            Details = details
        };
    }

    private static AdminRecipeService CreateService(AppDbContext context)
    {
        var physical = new PhysicalUnitConversionService(
            context,
            NullLogger<PhysicalUnitConversionService>.Instance);
        var clock = new FixedTimeProvider(new DateTimeOffset(BusinessInstantUtc));
        return new AdminRecipeService(
            context,
            new RecipeOutputNormalizer(context, physical),
            NullLogger<AdminRecipeService>.Instance,
            clock,
            new BusinessDateService(clock),
            new CurrentRecipeResolver(context));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
