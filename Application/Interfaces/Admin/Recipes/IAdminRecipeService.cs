using CafeChain.ViewModels.Admin.Recipes;
using CafeChain.Application.Results;
using System.Threading.Tasks;

namespace CafeChain.Application.Interfaces.Admin.Recipes
{
    public interface IAdminRecipeService
    {
        Task<ServiceResult> CreateRecipeAsync(RecipeCreateVM model);
    }
}
