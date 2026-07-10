using CafeChain.Models.Drinks;
using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.Sizes;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastrusture.Repositories.Admin.Sizes
{
    public class AdminSizeRepository : IAdminSizeRepository
    {
        private readonly AppDbContext _context;
        public AdminSizeRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Size>> GetAllAsync() =>
        await _context.Sizes
            .OrderBy(x => x.SizeId)
            .ToListAsync();

        public async Task<Size?> GetByIdAsync(int id) =>
            await _context.Sizes.FindAsync(id);

        public async Task AddAsync(Size size) => await _context.Sizes.AddAsync(size);

        public async Task UpdateAsync(Size size) => _context.Sizes.Update(size);

        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
        public async Task<bool> ExistsByNameAsync(string name)
        {
            name = name.Trim().ToLower();

            return await _context.Sizes
                .AsNoTracking()
                .AnyAsync(x => x.Name.ToLower() == name);
        }

        public async Task<bool> ExistsByNameAsync(string name, int excludeId)
        {
            name = name.Trim().ToLower();

            return await _context.Sizes
                .AsNoTracking()
                .AnyAsync(x => x.Name.ToLower() == name && x.SizeId != excludeId);
        }

        public async Task<bool> ExistsBySizeCodeAsync(string sizeCode)
        {
            sizeCode = sizeCode.Trim().ToLower();

            return await _context.Sizes
                .AsNoTracking()
                .AnyAsync(x => x.SizeCode.ToLower() == sizeCode);
        }

        public async Task<bool> ExistsBySizeCodeAsync(string sizeCode, int excludeId)
        {
            sizeCode = sizeCode.Trim().ToLower();

            return await _context.Sizes
                .AsNoTracking()
                .AnyAsync(x => x.SizeCode.ToLower() == sizeCode && x.SizeId != excludeId);
        }

        // ===== DRINK =====
        public async Task<IEnumerable<Drink>> GetActiveDrinksAsync()
        {
            return await _context.Drinks
                .AsNoTracking()
                .Where(d => d.Active)
                .Include(d => d.Category)
                .Include(d => d.ProductType)
                .Include(d => d.DrinkImages)
                .ToListAsync();
        }

        public async Task<Drink?> GetActiveDrinkByIdAsync(int drinkId)
        {
            return await _context.Drinks
                .AsNoTracking()
                .Include(d => d.ProductType)
                .FirstOrDefaultAsync(d => d.DrinkId == drinkId && d.Active);
        }
    }
}
