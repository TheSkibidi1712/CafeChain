using CafeChain.Models.Drinks;

namespace CafeChain.Application.DTOs.Admin.Recipes;

public abstract record RecipeTarget
{
    private RecipeTarget()
    {
    }

    public sealed record MenuItemSize(int DrinkId, int SizeId) : RecipeTarget;

    public sealed record Topping(int ToppingId) : RecipeTarget;

    public sealed record PreparedItem(int PreparedItemId) : RecipeTarget;
}

public enum CurrentRecipeResolutionStatus
{
    Found,
    Missing,
    Ambiguous,
    InvalidTarget
}

public sealed record CurrentRecipeResolution(
    CurrentRecipeResolutionStatus Status,
    Recipe? Recipe,
    string ReasonCode);
