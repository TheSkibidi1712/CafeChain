using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
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

        public async Task<IEnumerable<DrinkCategory>> GetAllCategoriesAsync()
        {
            return await _context.DrinkCategories.ToListAsync();
        }

        public async Task<DrinkCategory> GetCategoryByIdAsync(int id)
        {
            return await _context.DrinkCategories.FindAsync(id);
        }

        public async Task<(IEnumerable<DrinkCategory> Items, int TotalCount)> GetPaginatedCategoriesAsync(int pageIndex, int pageSize)
        {
            var query = _context.DrinkCategories.AsQueryable();
            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(c => c.CategoryId)
                                   .Skip((pageIndex - 1) * pageSize)
                                   .Take(pageSize)
                                   .ToListAsync();
            return (items, totalCount);
        }

        public async Task<DrinkCategory> CreateCategoryAsync(DrinkCategory category)
        {
            _context.DrinkCategories.Add(category);
            await _context.SaveChangesAsync();
            return category;
        }

        public async Task<DrinkCategory> UpdateCategoryAsync(DrinkCategory category)
        {
            _context.DrinkCategories.Update(category);
            await _context.SaveChangesAsync();
            return category;
        }

        public async Task<bool> CategoryExistsAsync(string name, int? excludeId = null)
        {
            if (excludeId.HasValue)
            {
                return await _context.DrinkCategories.AnyAsync(c => c.Name == name && c.CategoryId != excludeId.Value);
            }
            return await _context.DrinkCategories.AnyAsync(c => c.Name == name);
        }

        public async Task<bool> ToggleStatusAsync(int id)
        {
            var category = await _context.DrinkCategories.FindAsync(id);
            if (category == null) return false;

            category.Active = !category.Active;
            _context.DrinkCategories.Update(category);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
