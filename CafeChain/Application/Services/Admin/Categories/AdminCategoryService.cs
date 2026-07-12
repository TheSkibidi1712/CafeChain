using CafeChain.Application.DTOs.Admin.Categories;
using CafeChain.Application.Interfaces.Admin.Categories;
using CafeChain.Infrastrusture.Interfaces.Admin.Categories;
using CafeChain.Models.Drinks;
using CafeChain.ViewModels.Admin.Categories;
using CafeChain.ViewModels.Shared;
using CafeChain.Application.Services.AI;
using CafeChain.Application.Exceptions;

namespace CafeChain.Application.Services.Admin.Categories
{
    public class AdminCategoryService : IAdminCategoryService
    {
        private readonly IAdminCategoryRepository _repository;

        public AdminCategoryService(IAdminCategoryRepository repository)
        {
            _repository = repository;
        }

        // QUERIES METHODS

        public async Task<IEnumerable<AdminCategoryViewModel>> GetAllCategoriesAsync()
        {
            var categories = await _repository.GetAllCategoriesAsync();

            return categories.Select(MapToViewModel);
        }

        public async Task<AdminCategoryIndexViewModel> GetIndexDataAsync(CategoryFilterDto filter)
        {
            NormalizeFilter(filter);

            var result = await _repository.GetPaginatedCategoriesAsync(
                filter.Keyword,
                filter.Active,
                filter.Page,
                filter.PageSize);

            var items = result.Items
                .Select(MapToViewModel)
                .ToList();

            return new AdminCategoryIndexViewModel
            {
                Filter = filter,
                Categories = new PaginatedListViewModel<AdminCategoryViewModel>(
                    items,
                    result.TotalCount,
                    filter.Page,
                    filter.PageSize)
            };
        }

        public async Task<AdminCategoryViewModel?> GetCategoryByIdAsync(int id)
        {
            var category = await _repository.GetCategoryByIdAsync(id);

            return category == null
                ? null
                : MapToViewModel(category);
        }

        public async Task<bool> CheckCategoryNameExistAsync(string name, int? excludeId = null)
        {
            var result = await CheckCategoryUniquenessAsync(name, null, excludeId);
            return result.NameExists;
        }

        public async Task<(bool NameExists, bool CodeExists)> CheckCategoryUniquenessAsync(
            string name,
            string? code,
            int? excludeId = null,
            CancellationToken cancellationToken = default)
        {
            var categories = await _repository.GetAllCategoriesAsync(cancellationToken);
            var relevant = categories.Where(x => !excludeId.HasValue || x.CategoryId != excludeId.Value).ToList();
            var nameKey = AISuggestionUniquenessPolicy.NormalizeTextKey(name);
            var codeKey = AISuggestionUniquenessPolicy.NormalizeCodeKey(code);
            return (
                relevant.Any(x => AISuggestionUniquenessPolicy.NormalizeTextKey(x.Name) == nameKey),
                codeKey.Length > 0 && relevant.Any(x => AISuggestionUniquenessPolicy.NormalizeCodeKey(x.CategoryCode) == codeKey));
        }


        // COMMANDS METHODS

        public async Task<AdminCategoryViewModel> CreateCategoryAsync(AdminCreateCategoryDto dto)
        {
            var duplicate = await CheckCategoryUniquenessAsync(dto.Name, dto.CategoryCode);
            if (duplicate.NameExists || duplicate.CodeExists)
                throw BuildDuplicateException(duplicate.NameExists, duplicate.CodeExists);

            var category = BuildCategory(dto);

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

            var duplicate = await CheckCategoryUniquenessAsync(dto.Name, dto.CategoryCode, dto.CategoryId);
            if (duplicate.NameExists || duplicate.CodeExists)
                throw BuildDuplicateException(duplicate.NameExists, duplicate.CodeExists);

            UpdateCategoryEntity(category, dto);

            _repository.UpdateCategory(category);

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
                CategoryCode = category.CategoryCode,
                Name = category.Name,
                Icon = category.Icon,
                Active = category.Active
            };
        }

        public async Task<bool> ToggleCategoryStatusAsync(int id)
        {
            var success = await _repository.ToggleStatusAsync(id);

            if (!success)
            {
                return false;
            }

            await _repository.SaveChangesAsync();

            return true;
        }

        // PRIVATE METHODS

        private static void NormalizeFilter(CategoryFilterDto filter)
        {
            filter.Keyword = Normalize(filter.Keyword);

            filter.Page = filter.Page <= 0
                ? 1
                : filter.Page;

            filter.PageSize = filter.PageSize <= 0
                ? 10
                : filter.PageSize;
        }

        private static DrinkCategory BuildCategory(AdminCreateCategoryDto dto)
        {
            return new DrinkCategory
            {
                CategoryCode = Normalize(dto.CategoryCode),
                Name = Normalize(dto.Name),
                Icon = NormalizeOptional(dto.Icon),
                Active = dto.Active
            };
        }

        private static void UpdateCategoryEntity(DrinkCategory category, AdminUpdateCategoryDto dto)
        {
            category.CategoryCode = Normalize(dto.CategoryCode);

            category.Name = Normalize(dto.Name);

            category.Icon = NormalizeOptional(dto.Icon);

            category.Active = dto.Active;
        }

        private static string Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static AdminCategoryViewModel MapToViewModel(DrinkCategory category)
        {
            return new AdminCategoryViewModel
            {
                CategoryId = category.CategoryId,
                CategoryCode = category.CategoryCode,
                Name = category.Name,
                Icon = category.Icon,
                Active = category.Active
            };
        }

        private static DuplicateDataException BuildDuplicateException(bool nameExists, bool codeExists)
        {
            var message = nameExists && codeExists
                ? "Tên và mã danh mục đã tồn tại."
                : nameExists ? "Tên danh mục đã tồn tại." : "Mã danh mục đã tồn tại.";
            return new DuplicateDataException(message, nameExists ? "Name" : "CategoryCode");
        }

    }
}
