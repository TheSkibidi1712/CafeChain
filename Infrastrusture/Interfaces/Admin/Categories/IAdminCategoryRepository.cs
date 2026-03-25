using System.Threading.Tasks;
using CafeChain.Models.Drinks;

namespace CafeChain.Infrastrusture.Interfaces.Admin.Categories
{
    public interface IAdminCategoryRepository
    {
        Task<IEnumerable<DrinkCategory>> GetAllCategoriesAsync();
        Task<DrinkCategory> GetCategoryByIdAsync(int id);
        Task<(IEnumerable<DrinkCategory> Items, int TotalCount)> GetPaginatedCategoriesAsync(int pageIndex, int pageSize);
        Task<DrinkCategory> CreateCategoryAsync(DrinkCategory category);
        Task<DrinkCategory> UpdateCategoryAsync(DrinkCategory category);
        Task<bool> CategoryExistsAsync(string name, int? excludeId = null);
        Task<bool> ToggleStatusAsync(int id);
    }
}
