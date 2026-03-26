using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.Toppings;
using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
namespace CafeChain.Infrastrusture.Repositories.Admin.Toppings
{
    public class AdminToppingRepository : IAdminToppingRepository
    {
        private readonly AppDbContext _context;

        public AdminToppingRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Topping>> GetAllAsync()
        {
            return await _context.Toppings
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<Topping>> GetActiveAsync()
        {
            return await _context.Toppings
                .Where(x => x.Active)
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync();
        }

        public async Task<Topping?> GetByIdAsync(int id)
        {
            return await _context.Toppings.FindAsync(id);
        }

        public async Task AddAsync(Topping topping)
        {
            await _context.Toppings.AddAsync(topping);
        }

        public void Update(Topping topping)
        {
            _context.Toppings.Update(topping);
        }

        public async Task<bool> ExistsByNameAsync(string name)
        {
            name = name.Trim().ToLower();

            return await _context.Toppings
                .AnyAsync(x => x.Name.ToLower() == name);
        }

        public async Task<bool> ExistsByNameAsync(string name, int excludeId)
        {
            name = name.Trim().ToLower();

            return await _context.Toppings
                .AnyAsync(x => x.Name.ToLower() == name && x.ToppingId != excludeId);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
