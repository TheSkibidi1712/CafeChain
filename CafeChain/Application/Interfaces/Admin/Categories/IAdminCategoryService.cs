using System.Collections.Generic;
using System.Threading.Tasks;
using CafeChain.Application.DTOs.Admin.Categories;
using CafeChain.ViewModels.Admin.Categories;

namespace CafeChain.Application.Interfaces.Admin.Categories
{
    public interface IAdminCategoryService
    {
        Task<IEnumerable<AdminCategoryViewModel>> GetAllCategoriesAsync();
        Task<CafeChain.ViewModels.Shared.PaginatedListViewModel<AdminCategoryViewModel>> GetPaginatedCategoriesAsync(int pageIndex, int pageSize);
        Task<AdminCategoryViewModel> GetCategoryByIdAsync(int id);
        Task<AdminCategoryViewModel> CreateCategoryAsync(AdminCreateCategoryDto dto);
        Task<AdminCategoryViewModel> UpdateCategoryAsync(AdminUpdateCategoryDto dto);
        Task<bool> CheckCategoryNameExistAsync(string name, int? excludeId = null);
        Task<bool> ToggleCategoryStatusAsync(int id);
    }
}
