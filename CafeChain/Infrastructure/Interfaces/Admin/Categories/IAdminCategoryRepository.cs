using CafeChain.Models.Drinks;

namespace CafeChain.Infrastrusture.Interfaces.Admin.Categories
{
    public interface IAdminCategoryRepository
    {
        Task<IEnumerable<DrinkCategory>> GetAllCategoriesAsync(CancellationToken cancellationToken = default);

        Task<DrinkCategory?> GetCategoryByIdAsync(int id);

        Task<(IEnumerable<DrinkCategory> Items, int TotalCount)> GetPaginatedCategoriesAsync(string? keyword, bool? active, int pageIndex, int pageSize);

        Task CreateCategoryAsync(DrinkCategory category);

        void UpdateCategory(DrinkCategory category);

        Task<bool> CategoryExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);

        Task<bool> CategoryCodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);

        Task<bool> ToggleStatusAsync(int id);

        Task<int> SaveChangesAsync();
    }
}
