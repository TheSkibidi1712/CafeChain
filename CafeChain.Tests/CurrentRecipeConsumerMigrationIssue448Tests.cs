using CafeChain.Application.DTOs.Admin.Recipes;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Services.Admin.Profitability;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeChain.Tests;

public sealed class CurrentRecipeConsumerMigrationIssue448Tests : IntegrationTestBase
{
    private static readonly DateTime BusinessInstantUtc =
        new(2026, 8, 14, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AllRecipeSelectorsResolveSameCurrentVersion_AndAmbiguousTargetFailsSafely()
    {
        await using var context = CreateDbContext();
        var menuRecipe = await context.Recipes.SingleAsync(recipe => recipe.RecipeId == 1);
        var toppingRecipe = await context.Recipes.SingleAsync(recipe => recipe.RecipeId == 5);
        menuRecipe.EffectiveDate = BusinessInstantUtc.AddDays(-2);
        toppingRecipe.EffectiveDate = BusinessInstantUtc.AddDays(-2);

        context.Recipes.AddRange(
            new Recipe
            {
                RecipeCode = "ISSUE-448-SIZELESS",
                Name = "Công thức không gắn kích cỡ",
                DrinkId = menuRecipe.DrinkId,
                SizeId = null,
                Active = true,
                Status = "Active",
                EffectiveDate = BusinessInstantUtc.AddDays(-1)
            },
            new Recipe
            {
                RecipeCode = "ISSUE-448-TOPPING-DUPLICATE",
                Name = "Bằng chứng topping bị trùng",
                ToppingId = toppingRecipe.ToppingId,
                Active = true,
                Status = "Active",
                EffectiveDate = BusinessInstantUtc.AddDays(-1)
            });
        await context.SaveChangesAsync();

        var menuTarget = new RecipeTarget.MenuItemSize(
            menuRecipe.DrinkId!.Value,
            menuRecipe.SizeId!.Value);
        var toppingTarget = new RecipeTarget.Topping(toppingRecipe.ToppingId!.Value);
        var resolutions = await new CurrentRecipeResolver(context).ResolveManyAsync(
            new RecipeTarget[] { menuTarget, toppingTarget },
            BusinessInstantUtc);

        Assert.Equal(menuRecipe.RecipeId, resolutions[menuTarget].Recipe?.RecipeId);
        Assert.Equal(CurrentRecipeResolutionStatus.Ambiguous, resolutions[toppingTarget].Status);
        Assert.Null(resolutions[toppingTarget].Recipe);
        Assert.DoesNotContain(resolutions.Values, resolution =>
            resolution.Recipe?.RecipeCode == "ISSUE-448-SIZELESS");
    }

    [Fact]
    public async Task DrinkSizeHealthAdapter_UsesSharedCurrentRecipeAuthority()
    {
        await using var context = CreateDbContext();
        var source = await context.Recipes.SingleAsync(recipe => recipe.RecipeId == 1);
        var target = new RecipeTarget.MenuItemSize(source.DrinkId!.Value, source.SizeId!.Value);
        var authority = new StubCurrentRecipeResolver(new Dictionary<RecipeTarget, CurrentRecipeResolution>
        {
            [target] = new(
                CurrentRecipeResolutionStatus.Missing,
                null,
                CafeChain.Application.Constants.BomRecipeErrorCodes.CurrentRecipeMissing)
        });
        var physical = new PhysicalUnitConversionService(
            context,
            NullLogger<PhysicalUnitConversionService>.Instance);
        var unit = new UnitConversionService(
            context,
            NullLogger<UnitConversionService>.Instance,
            physical);
        var adapter = new DrinkSizeRecipeResolver(context, unit, physical, authority);

        var result = await adapter.ResolveExactAsync(
            source.DrinkId.Value,
            source.SizeId.Value,
            BusinessInstantUtc);

        Assert.False(result.IsReady);
        Assert.Null(result.Recipe);
        Assert.Equal(1, authority.SingleResolveCalls);
    }

    private sealed class StubCurrentRecipeResolver(
        IReadOnlyDictionary<RecipeTarget, CurrentRecipeResolution> resolutions)
        : ICurrentRecipeResolver
    {
        public int SingleResolveCalls { get; private set; }

        public Task<CurrentRecipeResolution> ResolveAsync(
            RecipeTarget target,
            DateTime businessInstantUtc,
            CancellationToken cancellationToken = default)
        {
            SingleResolveCalls++;
            return Task.FromResult(resolutions[target]);
        }

        public Task<IReadOnlyDictionary<RecipeTarget, CurrentRecipeResolution>> ResolveManyAsync(
            IReadOnlyCollection<RecipeTarget> targets,
            DateTime businessInstantUtc,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<RecipeTarget, CurrentRecipeResolution> selected = targets
                .Distinct()
                .ToDictionary(target => target, target => resolutions[target]);
            return Task.FromResult(selected);
        }
    }
}
