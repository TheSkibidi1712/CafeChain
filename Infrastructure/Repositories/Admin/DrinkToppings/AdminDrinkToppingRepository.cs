using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.DrinkToppings;
using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
namespace CafeChain.Infrastrusture.Repositories.Admin.DrinkToppings
{
    public class AdminDrinkToppingRepository : IAdminDrinkToppingRepository
    {
        private readonly AppDbContext _context;

        public AdminDrinkToppingRepository(AppDbContext context)
        {
            _context = context;
        }

        // =============================
        // GET ALL DRINK ACTIVE
        // =============================
        public async Task<IEnumerable<Drink>> GetActiveDrinksAsync()
        {
            return await _context.Drinks
                .Where(x => x.Active)
                .Include(x => x.Category)
                .Include(x => x.ProductType)
                .Include(x => x.DrinkImages)
                .AsNoTracking()
                .ToListAsync();
        }

        // =============================
        // GET DRINK-TOPPING BY TOPPING
        // =============================
        public async Task<IEnumerable<DrinkTopping>> GetByToppingIdAsync(int toppingId)
        {
            return await _context.DrinkToppings
                .Where(x => x.ToppingId == toppingId)
                .AsNoTracking()
                .ToListAsync();
        }

        // =============================
        // GET BY ID
        // =============================
        public async Task<DrinkTopping?> GetByIdAsync(int id)
        {
            return await _context.DrinkToppings
                .FirstOrDefaultAsync(x => x.DrinkToppingId == id);
        }

        // =============================
        // ADD
        // =============================
        public async Task AddAsync(DrinkTopping entity)
        {
            await _context.DrinkToppings.AddAsync(entity);
        }

        // =============================
        // UPDATE
        // =============================
        public Task UpdateAsync(DrinkTopping entity)
        {
            _context.DrinkToppings.Update(entity);
            return Task.CompletedTask;
        }

        // =============================
        // SAVE
        // =============================
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
