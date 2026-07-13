using System.Collections.Generic;
using System.Threading.Tasks;
using CafeChain.ViewModels.Admin.Recipes;

namespace CafeChain.Application.Interfaces.Admin.Recipes
{
    public interface IAdminRecipeQueryService
    {
        Task<AdminRecipeListPageVM> GetIndexPageAsync(string? typeFilter = null);

        Task<BomDataHealthPageVM> GetDataHealthPageAsync();

        Task<AdminRecipeFormPageVM> GetCreatePageAsync();

        Task<AdminRecipeFormPageVM?> GetEditPageAsync(int recipeId);

        Task<AdminRecipeVisualizePageVM?> GetVisualizePageAsync(int recipeId);

        Task<AdminRecipeFormOptionsVM> GetFormOptionsAsync();

        Task<List<RecipeSizeOptionVM>> GetSizesByDrinkAsync(int drinkId);
    }
}
