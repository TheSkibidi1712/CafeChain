using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public async Task<IEnumerable<AdminCategoryViewModel>> GetAllCategoriesAsync()
        {
            var categories = await _repository.GetAllCategoriesAsync();
            return categories.Select(c => new AdminCategoryViewModel
            {
                CategoryId = c.CategoryId,
                Name = c.Name,
                Active = c.Active
            });
        }

        public async Task<PaginatedListViewModel<AdminCategoryViewModel>> GetPaginatedCategoriesAsync(int pageIndex, int pageSize)
        {
            var result = await _repository.GetPaginatedCategoriesAsync(pageIndex, pageSize);
            
            var viewModels = result.Items.Select(c => new AdminCategoryViewModel
            {
                CategoryId = c.CategoryId,
                Name = c.Name,
                Active = c.Active
            }).ToList();

            return new PaginatedListViewModel<AdminCategoryViewModel>(viewModels, result.TotalCount, pageIndex, pageSize);
        }

        public async Task<AdminCategoryViewModel> GetCategoryByIdAsync(int id)
        {
            var category = await _repository.GetCategoryByIdAsync(id);
            if (category == null) return null;

            return new AdminCategoryViewModel
            {
                CategoryId = category.CategoryId,
                Name = category.Name,
                Active = category.Active
            };
        }

        public async Task<AdminCategoryViewModel> CreateCategoryAsync(AdminCreateCategoryDto dto)
        {
            var category = new DrinkCategory
            {
                Name = dto.Name,
                Active = dto.Active
            };

            var createdCategory = await _repository.CreateCategoryAsync(category);

            return new AdminCategoryViewModel
            {
                CategoryId = createdCategory.CategoryId,
                Name = createdCategory.Name,
                Active = createdCategory.Active
            };
        }

        public async Task<AdminCategoryViewModel> UpdateCategoryAsync(AdminUpdateCategoryDto dto)
        {
            var category = await _repository.GetCategoryByIdAsync(dto.CategoryId);
            if (category == null) return null;

            category.Name = dto.Name;
            category.Active = dto.Active;

            var updatedCategory = await _repository.UpdateCategoryAsync(category);

            return new AdminCategoryViewModel
            {
                CategoryId = updatedCategory.CategoryId,
                Name = updatedCategory.Name,
                Active = updatedCategory.Active
            };
        }

        public async Task<bool> CheckCategoryNameExistAsync(string name, int? excludeId = null)
        {
            return await _repository.CategoryExistsAsync(name, excludeId);
        }

        public async Task<bool> ToggleCategoryStatusAsync(int id)
        {
            return await _repository.ToggleStatusAsync(id);
        }
    }
}
