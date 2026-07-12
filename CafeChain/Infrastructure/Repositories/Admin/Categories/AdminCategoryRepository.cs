using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.Categories;
using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using CafeChain.Application.Exceptions;

namespace CafeChain.Infrastrusture.Repositories.Admin.Categories
{
    public class AdminCategoryRepository : IAdminCategoryRepository
    {
        private readonly AppDbContext _context;

        public AdminCategoryRepository(AppDbContext context)
        {
            _context = context;
        }

        // QUERIES METHODS

        public async Task<IEnumerable<DrinkCategory>> GetAllCategoriesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.DrinkCategories
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<DrinkCategory?> GetCategoryByIdAsync(int id)
        {
            return await _context.DrinkCategories
                .FirstOrDefaultAsync(c => c.CategoryId == id);
        }

        public async Task<(IEnumerable<DrinkCategory> Items, int TotalCount)> GetPaginatedCategoriesAsync(string? keyword, bool? active, int pageIndex, int pageSize)
        {
            IQueryable<DrinkCategory> query = _context.DrinkCategories.AsNoTracking();

            query = ApplyFilters(
                query,
                keyword,
                active);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.CategoryId)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<bool> CategoryExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
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

            return await query.AnyAsync(cancellationToken);
        }

        public async Task<bool> CategoryCodeExistsAsync(
            string code,
            int? excludeId = null,
            CancellationToken cancellationToken = default)
        {
            code = code.Trim();
            var query = _context.DrinkCategories.AsNoTracking().Where(x => x.CategoryCode == code);
            if (excludeId.HasValue) query = query.Where(x => x.CategoryId != excludeId.Value);
            return await query.AnyAsync(cancellationToken);
        }


        // CRUD OPERATIONS

        public async Task CreateCategoryAsync(
            DrinkCategory category)
        {
            await _context.DrinkCategories.AddAsync(category);
        }

        public void UpdateCategory(DrinkCategory category)
        {
            _context.DrinkCategories.Update(category);
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
            try
            {
                return await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                throw new DuplicateDataException(
                    "Tên hoặc mã danh mục đã tồn tại. Vui lòng tải lại dữ liệu và thử lại.",
                    innerException: ex);
            }
        }

        private static bool IsUniqueConstraintViolation(Exception exception)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
                if (current is SqlException sql && sql.Number is 2601 or 2627) return true;
            return false;
        }

        // PRIVATE METHODS
        private static IQueryable<DrinkCategory> ApplyFilters(IQueryable<DrinkCategory> query, string? keyword, bool? active)
        {
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();

                query = query.Where(x =>
                    x.CategoryCode.Contains(keyword) ||
                    x.Name.Contains(keyword));
            }

            if (active.HasValue)
            {
                query = query.Where(x =>
                    x.Active == active.Value);
            }

            return query;
        }
    }
}
