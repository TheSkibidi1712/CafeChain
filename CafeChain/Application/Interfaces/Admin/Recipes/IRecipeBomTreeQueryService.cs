using System.Threading.Tasks;
using CafeChain.ViewModels.Admin.Recipes;

namespace CafeChain.Application.Interfaces.Admin.Recipes
{
    public interface IRecipeBomTreeQueryService
    {
        Task<RecipeBomTreeResult> BuildTreeAsync(int rootRecipeId, int maxDepth = 5);
    }
}
