using CafeChain.Application.DTOs.Admin.Recipes;

namespace CafeChain.Application.Interfaces.Admin.Recipes;

public interface ICurrentRecipeResolver
{
    Task<CurrentRecipeResolution> ResolveAsync(
        RecipeTarget target,
        DateTime businessInstantUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<RecipeTarget, CurrentRecipeResolution>> ResolveManyAsync(
        IReadOnlyCollection<RecipeTarget> targets,
        DateTime businessInstantUtc,
        CancellationToken cancellationToken = default);
}
