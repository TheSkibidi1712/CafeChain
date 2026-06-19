using CafeChain.Models.Drinks;

namespace CafeChain.Infrastrusture.Interfaces.Admin.Categories
{
    public interface IAdminCategoryRepository
    {
        Task<IEnumerable<DrinkCategory>> GetAllCategoriesAsync();

        Task<DrinkCategory?> GetCategoryByIdAsync(int id);

        Task<(IEnumerable<DrinkCategory> Items, int TotalCount)> GetPaginatedCategoriesAsync(int pageIndex, int pageSize);

        Task CreateCategoryAsync(DrinkCategory category);

        Task UpdateCategoryAsync(DrinkCategory category);

        Task<bool> CategoryExistsAsync(string name, int? excludeId = null);

        Task<bool> ToggleStatusAsync(int id);

        Task<int> SaveChangesAsync();
    }
}