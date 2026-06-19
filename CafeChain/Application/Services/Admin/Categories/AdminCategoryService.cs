using CafeChain.Application.DTOs.Admin.Categories;
using CafeChain.Application.Interfaces.Admin.Categories;
using CafeChain.Infrastrusture.Interfaces.Admin.Categories;
using CafeChain.Models.Drinks;
using CafeChain.ViewModels.Admin.Categories;
using CafeChain.ViewModels.Shared;

namespace CafeChain.Application.Services.Admin.Categories
{
    public class AdminCategoryService : IAdminCategoryService
    {
        private readonly IAdminCategoryRepository _repository;

        public AdminCategoryService(IAdminCategoryRepository repository)
        {
            _repository = repository;
        }

        #region Queries

        public async Task<IEnumerable<AdminCategoryViewModel>> GetAllCategoriesAsync()
        {
            var categories = await _repository.GetAllCategoriesAsync();

            return categories.Select(MapToViewModel);
        }

        public async Task<PaginatedListViewModel<AdminCategoryViewModel>> GetPaginatedCategoriesAsync(int pageIndex, int pageSize)
        {
            var result = await _repository.GetPaginatedCategoriesAsync(pageIndex, pageSize);

            var items = result.Items.Select(MapToViewModel).ToList();

            return new PaginatedListViewModel<AdminCategoryViewModel>(items, result.TotalCount, pageIndex, pageSize);
        }

        public async Task<AdminCategoryViewModel?> GetCategoryByIdAsync(int id)
        {
            var category = await _repository.GetCategoryByIdAsync(id);

            return category == null ? null : MapToViewModel(category);
        }

        public async Task<bool> CheckCategoryNameExistAsync(string name, int? excludeId = null)
        {
            name = NormalizeName(name);

            return await _repository.CategoryExistsAsync(name, excludeId);
        }

        #endregion

        #region Commands

        public async Task<AdminCategoryViewModel> CreateCategoryAsync(AdminCreateCategoryDto dto)
        {
            var category = new DrinkCategory
            {
                Name = NormalizeName(dto.Name),
                Active = dto.Active
            };

            await _repository.CreateCategoryAsync(category);

            await _repository.SaveChangesAsync();

            return MapToViewModel(category);
        }

        public async Task<AdminCategoryViewModel?> UpdateCategoryAsync(AdminUpdateCategoryDto dto)
        {
            var category = await _repository.GetCategoryByIdAsync(dto.CategoryId);

            if (category == null)
            {
                return null;
            }

            category.Name = NormalizeName(dto.Name);
            category.Active = dto.Active;

            await _repository.UpdateCategoryAsync(category);

            await _repository.SaveChangesAsync();

            return MapToViewModel(category);
        }

        public async Task<AdminUpdateCategoryDto?> GetCategoryForEditAsync(int id)
        {
            var category = await _repository.GetCategoryByIdAsync(id);

            if (category == null)
            {
                return null;
            }

            return new AdminUpdateCategoryDto
            {
                CategoryId = category.CategoryId,
                Name = category.Name,
                Active = category.Active
            };
        }

        public async Task<bool> ToggleCategoryStatusAsync(int id)
        {
            var result = await _repository.ToggleStatusAsync(id);

            if (!result)
            {
                return false;
            }

            await _repository.SaveChangesAsync();

            return true;
        }

        #endregion

        #region Private Methods

        private static string NormalizeName(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
        }

        private static AdminCategoryViewModel MapToViewModel(DrinkCategory category)
        {
            return new AdminCategoryViewModel
            {
                CategoryId = category.CategoryId,
                Name = category.Name,
                Active = category.Active
            };
        }

        #endregion
    }
}