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

        public async Task<IEnumerable<Drink>> GetAllDrinksAsync()
        {
            return await _context.Drinks
                .Include(d => d.Category)
                .Include(d => d.ProductType)
                .Include(d => d.DrinkImages)
                .OrderByDescending(d => d.DrinkId)
                .ToListAsync();
        }

        public async Task<Drink> GetDrinkByIdAsync(int id)
        {
            return await _context.Drinks
                .Include(d => d.Category)
                .Include(d => d.ProductType)
                .Include(d => d.DrinkImages)
                .FirstOrDefaultAsync(d => d.DrinkId == id);
        }

        public async Task<int> CreateDrinkAsync(Drink drink)
        {
            _context.Drinks.Add(drink);
            await _context.SaveChangesAsync();
            return drink.DrinkId;
        }

        public async Task UpdateDrinkAsync(Drink drink)
        {
            // Đã nhận được entity Tracking từ DbContext qua hàm GetDrinkByIdAsync, 
            // EF Core tự phát hiện thay đổi nên chỉ cần SaveChanges. Không được dùng .Update() vì sẽ gây conflict các Navigation Properties.
            await _context.SaveChangesAsync();
        }

        public async Task ToggleDrinkStatusAsync(int id)
        {
            var drink = await _context.Drinks.FindAsync(id);
            if (drink != null)
            {
                drink.Active = !drink.Active;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> IsDrinkNameExistsAsync(string name, int? excludeId = null)
        {
            var query = _context.Drinks.Where(d => d.Name.ToLower() == name.ToLower());
            if (excludeId.HasValue)
            {
                query = query.Where(d => d.DrinkId != excludeId.Value);
            }
            return await query.AnyAsync();
        }

        public async Task<IEnumerable<DrinkCategory>> GetDrinkCategoriesAsync()
        {
            return await _context.DrinkCategories
                .Where(c => c.Active)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<ProductType>> GetProductTypesAsync()
        {
            return await _context.ProductTypes
                .Where(pt => pt.Active)
                .OrderBy(pt => pt.Name)
                .ToListAsync();
        }

        // Image Management

        public async Task<IEnumerable<DrinkImage>> GetDrinkImagesAsync(int drinkId)
        {
            return await _context.DrinkImages
                .Where(img => img.DrinkId == drinkId)
                .OrderByDescending(img => img.IsDefault)
                .ThenByDescending(img => img.DrinkImageId)
                .ToListAsync();
        }

        public async Task<DrinkImage> GetDrinkImageByIdAsync(int drinkImageId)
        {
            return await _context.DrinkImages.FindAsync(drinkImageId);
        }

        public async Task AddDrinkImageAsync(DrinkImage drinkImage)
        {
            await _context.DrinkImages.AddAsync(drinkImage);
            await _context.SaveChangesAsync(); // 🔥 BẮT BUỘC

        }

        public async Task SetDefaultDrinkImageAsync(int drinkId, int newDefaultImageId)
        {
            // Reset existing defaults
            var currentDefaultImages = await _context.DrinkImages
                .Where(img => img.DrinkId == drinkId && img.IsDefault)
                .ToListAsync();

            foreach (var img in currentDefaultImages)
            {
                img.IsDefault = false;
            }

            // Set new target as default
            var targetImage = await _context.DrinkImages.FindAsync(newDefaultImageId);
            if (targetImage != null && targetImage.DrinkId == drinkId)
            {
                targetImage.IsDefault = true;
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteDrinkImageAsync(int drinkImageId)
        {
            var image = await _context.DrinkImages.FindAsync(drinkImageId);
            if (image != null)
            {
                _context.DrinkImages.Remove(image);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateDrinkImageAsync(DrinkImage drinkImage)
        {
            // Entity đã được track bởi DbContext (lấy qua FindAsync),
            // chỉ cần cập nhật ImageUrl rồi SaveChanges là đủ.
            await _context.SaveChangesAsync();
        }
    }
}
