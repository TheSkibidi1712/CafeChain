using CafeChain.Application.DTOs.Admin.Categories;
using CafeChain.ViewModels.Admin.Categories;
using CafeChain.ViewModels.Shared;

namespace CafeChain.Application.Interfaces.Admin.Categories
{
    public interface IAdminCategoryService
    {
        Task<IEnumerable<AdminCategoryViewModel>> GetAllCategoriesAsync();

        Task<PaginatedListViewModel<AdminCategoryViewModel>> GetPaginatedCategoriesAsync(int pageIndex, int pageSize);

        Task<AdminCategoryViewModel?> GetCategoryByIdAsync(int id);

        Task<AdminCategoryViewModel> CreateCategoryAsync(AdminCreateCategoryDto dto);

        Task<AdminUpdateCategoryDto?> GetCategoryForEditAsync(int id);

        Task<AdminCategoryViewModel?> UpdateCategoryAsync(AdminUpdateCategoryDto dto);

        Task<bool> CheckCategoryNameExistAsync(string name, int? excludeId = null);

        Task<bool> ToggleCategoryStatusAsync(int id);

    }
}