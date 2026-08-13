using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Recipes;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Recipes;

public sealed class CurrentRecipeResolver : ICurrentRecipeResolver
{
    private readonly AppDbContext _context;

    public CurrentRecipeResolver(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CurrentRecipeResolution> ResolveAsync(
        RecipeTarget target,
        DateTime businessInstantUtc,
        CancellationToken cancellationToken = default)
    {
        var resolutions = await ResolveManyAsync(
            new[] { target },
            businessInstantUtc,
            cancellationToken);
        return resolutions[target];
    }

    public async Task<IReadOnlyDictionary<RecipeTarget, CurrentRecipeResolution>> ResolveManyAsync(
        IReadOnlyCollection<RecipeTarget> targets,
        DateTime businessInstantUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targets);

        var requestedTargets = targets.Distinct().ToArray();
        var resolutions = new Dictionary<RecipeTarget, CurrentRecipeResolution>(requestedTargets.Length);
        if (requestedTargets.Length == 0)
            return resolutions;

        foreach (var target in requestedTargets)
        {
            resolutions[target] = businessInstantUtc.Kind == DateTimeKind.Utc && IsValid(target)
                ? Missing()
                : Invalid();
        }

        if (businessInstantUtc.Kind != DateTimeKind.Utc)
            return resolutions;

        var validTargets = requestedTargets.Where(IsValid).ToHashSet();
        if (validTargets.Count == 0)
            return resolutions;

        var currentRecipes = _context.Recipes
            .AsNoTracking()
            .Where(recipe =>
                recipe.Active
                && recipe.Status == "Active"
                && (!recipe.EffectiveDate.HasValue || recipe.EffectiveDate.Value <= businessInstantUtc));

        IQueryable<Recipe>? candidates = null;

        var menuTargets = validTargets.OfType<RecipeTarget.MenuItemSize>().ToArray();
        if (menuTargets.Length > 0)
        {
            var drinkIds = menuTargets.Select(target => target.DrinkId).Distinct().ToArray();
            var sizeIds = menuTargets.Select(target => target.SizeId).Distinct().ToArray();
            candidates = currentRecipes.Where(recipe =>
                recipe.DrinkId.HasValue
                && drinkIds.Contains(recipe.DrinkId.Value)
                && recipe.SizeId.HasValue
                && sizeIds.Contains(recipe.SizeId.Value)
                && recipe.ToppingId == null
                && recipe.PreparedItemId == null);
        }

        var toppingTargets = validTargets.OfType<RecipeTarget.Topping>().ToArray();
        if (toppingTargets.Length > 0)
        {
            var toppingIds = toppingTargets.Select(target => target.ToppingId).Distinct().ToArray();
            var toppingCandidates = currentRecipes.Where(recipe =>
                recipe.ToppingId.HasValue
                && toppingIds.Contains(recipe.ToppingId.Value)
                && recipe.DrinkId == null
                && recipe.SizeId == null
                && recipe.PreparedItemId == null);
            candidates = candidates == null ? toppingCandidates : candidates.Concat(toppingCandidates);
        }

        var preparedTargets = validTargets.OfType<RecipeTarget.PreparedItem>().ToArray();
        if (preparedTargets.Length > 0)
        {
            var preparedItemIds = preparedTargets
                .Select(target => target.PreparedItemId)
                .Distinct()
                .ToArray();
            var preparedCandidates = currentRecipes.Where(recipe =>
                recipe.PreparedItemId.HasValue
                && preparedItemIds.Contains(recipe.PreparedItemId.Value)
                && recipe.DrinkId == null
                && recipe.SizeId == null
                && recipe.ToppingId == null);
            candidates = candidates == null ? preparedCandidates : candidates.Concat(preparedCandidates);
        }

        if (candidates == null)
            return resolutions;

        var rows = await candidates
            .ToListAsync(cancellationToken);

        foreach (var group in rows
                     .Select(recipe => (Target: CreateTarget(recipe), Recipe: recipe))
                     .Where(candidate => candidate.Target != null && validTargets.Contains(candidate.Target))
                     .GroupBy(candidate => candidate.Target!))
        {
            var matches = group.Take(2).Select(candidate => candidate.Recipe).ToArray();
            resolutions[group.Key] = matches.Length == 1
                ? Found(matches[0])
                : Ambiguous();
        }

        return resolutions;
    }

    private static bool IsValid(RecipeTarget target)
    {
        return target switch
        {
            RecipeTarget.MenuItemSize menu => menu.DrinkId > 0 && menu.SizeId > 0,
            RecipeTarget.Topping topping => topping.ToppingId > 0,
            RecipeTarget.PreparedItem preparedItem => preparedItem.PreparedItemId > 0,
            _ => false
        };
    }

    private static RecipeTarget? CreateTarget(Recipe recipe)
    {
        if (recipe.DrinkId.HasValue && recipe.SizeId.HasValue
            && !recipe.ToppingId.HasValue && !recipe.PreparedItemId.HasValue)
        {
            return new RecipeTarget.MenuItemSize(recipe.DrinkId.Value, recipe.SizeId.Value);
        }

        if (recipe.ToppingId.HasValue && !recipe.DrinkId.HasValue
            && !recipe.SizeId.HasValue && !recipe.PreparedItemId.HasValue)
        {
            return new RecipeTarget.Topping(recipe.ToppingId.Value);
        }

        if (recipe.PreparedItemId.HasValue && !recipe.DrinkId.HasValue
            && !recipe.SizeId.HasValue && !recipe.ToppingId.HasValue)
        {
            return new RecipeTarget.PreparedItem(recipe.PreparedItemId.Value);
        }

        return null;
    }

    private static CurrentRecipeResolution Found(Recipe recipe) => new(
        CurrentRecipeResolutionStatus.Found,
        recipe,
        string.Empty);

    private static CurrentRecipeResolution Missing() => new(
        CurrentRecipeResolutionStatus.Missing,
        null,
        BomRecipeErrorCodes.CurrentRecipeMissing);

    private static CurrentRecipeResolution Ambiguous() => new(
        CurrentRecipeResolutionStatus.Ambiguous,
        null,
        BomRecipeErrorCodes.CurrentRecipeAmbiguous);

    private static CurrentRecipeResolution Invalid() => new(
        CurrentRecipeResolutionStatus.InvalidTarget,
        null,
        BomRecipeErrorCodes.CurrentRecipeInvalidTarget);
}
