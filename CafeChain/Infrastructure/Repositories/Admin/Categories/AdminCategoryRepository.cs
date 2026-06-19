using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.Categories;
using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastrusture.Repositories.Admin.Categories
{
    public class AdminCategoryRepository : IAdminCategoryRepository
    {
        private readonly AppDbContext _context;

        public AdminCategoryRepository(AppDbContext context)
        {
            _context = context;
        }

        #region Queries

        public async Task<IEnumerable<DrinkCategory>> GetAllCategoriesAsync()
        {
            return await _context.DrinkCategories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<DrinkCategory?> GetCategoryByIdAsync(int id)
        {
            return await _context.DrinkCategories
                .FirstOrDefaultAsync(c => c.CategoryId == id);
        }

        public async Task<(IEnumerable<DrinkCategory> Items, int TotalCount)>
            GetPaginatedCategoriesAsync(int pageIndex, int pageSize)
        {
            var query = _context.DrinkCategories
                .AsNoTracking();

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(c => c.CategoryId)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<bool> CategoryExistsAsync(
            string name,
            int? excludeId = null)
        {
            name = name.Trim();

            var query = _context.DrinkCategories
                .AsNoTracking()
                .Where(c => c.Name == name);

            if (excludeId.HasValue)
            {
                query = query.Where(c =>
                    c.CategoryId != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        #endregion

        #region Commands

        public async Task CreateCategoryAsync(
            DrinkCategory category)
        {
            await _context.DrinkCategories.AddAsync(category);
        }

        public Task UpdateCategoryAsync(
            DrinkCategory category)
        {
            _context.DrinkCategories.Update(category);

            return Task.CompletedTask;
        }

        public async Task<bool> ToggleStatusAsync(int id)
        {
            var category = await _context.DrinkCategories
                .FirstOrDefaultAsync(c => c.CategoryId == id);

            if (category == null)
            {
                return false;
            }

            category.Active = !category.Active;

            return true;
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }

        #endregion
    }
}