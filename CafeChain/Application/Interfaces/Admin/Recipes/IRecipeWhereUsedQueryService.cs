using CafeChain.ViewModels.Admin.Recipes;

namespace CafeChain.Application.Interfaces.Admin.Recipes;

public interface IRecipeWhereUsedQueryService
{
    Task<RecipeWhereUsedVM> GetCurrentAsync(
        int recipeId,
        IReadOnlyCollection<int> authorizedStoreIds,
        CancellationToken cancellationToken = default);
}
