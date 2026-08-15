using CafeChain.ViewModels.Admin.Recipes;

namespace CafeChain.Application.Interfaces.Admin.Recipes;

public interface IRecipeVersionEvidenceQueryService
{
    Task<RecipeVersionHistoryVM> GetHistoryAsync(
        int recipeId,
        CancellationToken cancellationToken = default);

    Task<RecipeVersionCompareResult> CompareAsync(
        int fromRecipeId,
        int toRecipeId,
        CancellationToken cancellationToken = default);
}
