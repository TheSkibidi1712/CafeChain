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
        if (businessInstantUtc.Kind != DateTimeKind.Utc || !IsValid(target))
        {
            return new CurrentRecipeResolution(
                CurrentRecipeResolutionStatus.InvalidTarget,
                null,
                BomRecipeErrorCodes.CurrentRecipeInvalidTarget);
        }

        IQueryable<Recipe> query = _context.Recipes
            .AsNoTracking()
            .Where(recipe =>
                recipe.Active
                && recipe.Status == "Active"
                && (!recipe.EffectiveDate.HasValue || recipe.EffectiveDate.Value <= businessInstantUtc));

        query = target switch
        {
            RecipeTarget.MenuItemSize menu => query.Where(recipe =>
                recipe.DrinkId == menu.DrinkId
                && recipe.SizeId == menu.SizeId
                && recipe.ToppingId == null
                && recipe.PreparedItemId == null),
            RecipeTarget.Topping topping => query.Where(recipe =>
                recipe.ToppingId == topping.ToppingId
                && recipe.DrinkId == null
                && recipe.SizeId == null
                && recipe.PreparedItemId == null),
            RecipeTarget.PreparedItem preparedItem => query.Where(recipe =>
                recipe.PreparedItemId == preparedItem.PreparedItemId
                && recipe.DrinkId == null
                && recipe.SizeId == null
                && recipe.ToppingId == null),
            _ => query.Where(_ => false)
        };

        var matches = await query
            .OrderByDescending(recipe => recipe.EffectiveDate)
            .ThenByDescending(recipe => recipe.RecipeId)
            .Take(2)
            .ToListAsync(cancellationToken);

        return matches.Count switch
        {
            0 => new CurrentRecipeResolution(
                CurrentRecipeResolutionStatus.Missing,
                null,
                BomRecipeErrorCodes.CurrentRecipeMissing),
            1 => new CurrentRecipeResolution(
                CurrentRecipeResolutionStatus.Found,
                matches[0],
                string.Empty),
            _ => new CurrentRecipeResolution(
                CurrentRecipeResolutionStatus.Ambiguous,
                null,
                BomRecipeErrorCodes.CurrentRecipeAmbiguous)
        };
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
}
