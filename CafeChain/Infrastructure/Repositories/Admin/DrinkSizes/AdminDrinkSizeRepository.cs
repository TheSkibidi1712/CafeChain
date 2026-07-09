using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.DrinkSizes;
using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
namespace CafeChain.Infrastrusture.Repositories.Admin.DrinkSizes
{
    public class AdminDrinkSizeRepository : IAdminDrinkSizeRepository
    {
        private readonly AppDbContext _context;

        public AdminDrinkSizeRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<DrinkSize>> GetBySizeIdAsync(int sizeId)
        {
            return await _context.DrinkSizes
                .Where(x => x.SizeId == sizeId)
                .ToListAsync();
        }

        public async Task<DrinkSize?> GetByIdAsync(int id)
        {
            return await _context.DrinkSizes
                .FirstOrDefaultAsync(x => x.DrinkSizeId == id);
        }

        public async Task<DrinkSize?> GetByDrinkAndSizeAsync(int drinkId, int sizeId)
        {
            return await _context.DrinkSizes
                .FirstOrDefaultAsync(x =>
                    x.DrinkId == drinkId &&
                    x.SizeId == sizeId);
        }

        public async Task AddAsync(DrinkSize entity)
        {
            await _context.DrinkSizes.AddAsync(entity);
        }

        public async Task UpdateAsync(DrinkSize entity)
        {
            _context.DrinkSizes.Update(entity);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
