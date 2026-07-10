using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.Drinks;
using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastrusture.Repositories.Admin.Drinks
{
    public class AdminDrinkRepository : IAdminDrinkRepository
    {
        private readonly AppDbContext _context;

        public AdminDrinkRepository(AppDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // Drink
        // =====================================================

        public async Task<IEnumerable<Drink>> GetAllDrinksAsync()
        {
            return await _context.Drinks
                .Include(x => x.Category)
                .Include(x => x.ProductType)
                .Include(x => x.DrinkImages)
                .OrderByDescending(x => x.DrinkId)
                .ToListAsync();
        }

        public async Task<(IEnumerable<Drink> Items, int TotalCount)> GetPaginatedDrinksAsync(
            string? keyword,
            bool? active,
            int pageIndex,
            int pageSize)
        {
            IQueryable<Drink> query = _context.Drinks
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.ProductType)
                .Include(x => x.DrinkImages);

            query = ApplyFilters(query, keyword, active);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.DrinkId)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(int TotalCount, int ActiveCount, int InactiveCount)> GetDrinkCountsAsync(string? keyword)
        {
            IQueryable<Drink> query = _context.Drinks
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.ProductType);

            query = ApplyKeywordFilter(query, keyword);

            var totalCount = await query.CountAsync();
            var activeCount = await query.CountAsync(x => x.Active);

            return (totalCount, activeCount, totalCount - activeCount);
        }

        public async Task<Drink?> GetDrinkByIdAsync(int id)
        {
            return await _context.Drinks
                .Include(x => x.Category)
                .Include(x => x.ProductType)
                .Include(x => x.DrinkImages)
                .FirstOrDefaultAsync(x => x.DrinkId == id);
        }

        public async Task<int> CreateDrinkAsync(Drink drink)
        {
            await _context.Drinks.AddAsync(drink);

            return drink.DrinkId;
        }

        public Task UpdateDrinkAsync(Drink drink)
        {
            _context.Drinks.Update(drink);

            return Task.CompletedTask;
        }

        public async Task ToggleDrinkStatusAsync(int id)
        {
            var drink = await _context.Drinks
                .FirstOrDefaultAsync(x => x.DrinkId == id);

            if (drink == null)
            {
                return;
            }

            drink.Active = !drink.Active;
        }

        public async Task<bool> IsDrinkNameExistsAsync(
            string name,
            int? excludeId = null)
        {
            name = name.Trim();

            var query = _context.Drinks
                .AsNoTracking()
                .Where(x => x.Name.ToLower() == name.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(x => x.DrinkId != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<bool> IsDrinkCodeExistsAsync(
            string drinkCode,
            int? excludeId = null)
        {
            drinkCode = drinkCode.Trim();

            var query = _context.Drinks
                .AsNoTracking()
                .Where(x => x.DrinkCode.ToLower() == drinkCode.ToLower());

            if (excludeId.HasValue)
            {
                query = query.Where(x => x.DrinkId != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<IEnumerable<DrinkCategory>> GetDrinkCategoriesAsync()
        {
            return await _context.DrinkCategories
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<ProductType>> GetProductTypesAsync()
        {
            return await _context.ProductTypes
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .ToListAsync();
        }

        // =====================================================
        // Drink Images
        // =====================================================

        public async Task<IEnumerable<DrinkImage>> GetDrinkImagesAsync(int drinkId)
        {
            return await _context.DrinkImages
                .Where(x => x.DrinkId == drinkId)
                .OrderByDescending(x => x.IsDefault)
                .ThenByDescending(x => x.DrinkImageId)
                .ToListAsync();
        }

        public async Task<DrinkImage?> GetDrinkImageByIdAsync(int drinkImageId)
        {
            return await _context.DrinkImages
                .FirstOrDefaultAsync(x => x.DrinkImageId == drinkImageId);
        }

        public async Task AddDrinkImageAsync(DrinkImage drinkImage)
        {
            await _context.DrinkImages.AddAsync(drinkImage);
        }

        public Task UpdateDrinkImageAsync(DrinkImage drinkImage)
        {
            _context.DrinkImages.Update(drinkImage);

            return Task.CompletedTask;
        }

        public async Task DeleteDrinkImageAsync(int drinkImageId)
        {
            var image = await _context.DrinkImages
                .FirstOrDefaultAsync(x => x.DrinkImageId == drinkImageId);

            if (image == null)
            {
                return;
            }

            _context.DrinkImages.Remove(image);
        }

        public async Task SetDefaultDrinkImageAsync(
            int drinkId,
            int drinkImageId)
        {
            var images = await _context.DrinkImages
                .Where(x => x.DrinkId == drinkId)
                .ToListAsync();

            foreach (var image in images)
            {
                image.IsDefault = false;
            }

            var targetImage = images
                .FirstOrDefault(x => x.DrinkImageId == drinkImageId);

            if (targetImage != null)
            {
                targetImage.IsDefault = true;
            }
        }

        public async Task<bool> HasDefaultImageAsync(int drinkId)
        {
            return await _context.DrinkImages
                .AnyAsync(x =>
                    x.DrinkId == drinkId &&
                    x.IsDefault);
        }

        public async Task<int> GetImageCountAsync(int drinkId)
        {
            return await _context.DrinkImages
                .CountAsync(x => x.DrinkId == drinkId);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        private static IQueryable<Drink> ApplyFilters(
            IQueryable<Drink> query,
            string? keyword,
            bool? active)
        {
            query = ApplyKeywordFilter(query, keyword);

            if (active.HasValue)
            {
                query = query.Where(x => x.Active == active.Value);
            }

            return query;
        }

        private static IQueryable<Drink> ApplyKeywordFilter(
            IQueryable<Drink> query,
            string? keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return query;
            }

            keyword = keyword.Trim();

            return query.Where(x =>
                x.DrinkCode.Contains(keyword) ||
                x.Name.Contains(keyword) ||
                (x.Category != null && x.Category.Name.Contains(keyword)) ||
                (x.ProductType != null && x.ProductType.Name.Contains(keyword)));
        }
    }
}
