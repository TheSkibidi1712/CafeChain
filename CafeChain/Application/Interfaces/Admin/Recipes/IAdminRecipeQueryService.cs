using System.Collections.Generic;
using System.Threading.Tasks;
using CafeChain.ViewModels.Admin.Recipes;

namespace CafeChain.Application.Interfaces.Admin.Recipes
{
    public interface IAdminRecipeQueryService
    {
        Task<AdminRecipeListPageVM> GetIndexPageAsync(
            string? typeFilter = null,
            string? search = null,
            string? status = null,
            int page = 1,
            int pageSize = 15);

        Task<BomDataHealthPageVM> GetDataHealthPageAsync();

        Task<AdminRecipeFormPageVM> GetCreatePageAsync();

        Task<AdminRecipeFormPageVM?> GetEditPageAsync(int recipeId);

        Task<AdminRecipeVisualizePageVM?> GetVisualizePageAsync(int recipeId);

        Task<BomOperationalDetailVM?> GetOperationalDetailAsync(int recipeId, int storeId);

        Task<IReadOnlyDictionary<int, ToppingConsumptionSourceVM>> GetToppingConsumptionSourcesAsync(
            IEnumerable<int> toppingIds);

        Task<AdminRecipeFormOptionsVM> GetFormOptionsAsync();

        Task<List<RecipeSizeOptionVM>> GetSizesByDrinkAsync(int drinkId);
    }
}
