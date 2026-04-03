using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.Ingredients;
using CafeChain.Models.Inventories;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastrusture.Repositories.Admin.Ingredients
{
    public class AdminIngredientRepository : IAdminIngredientRepository
    {
        private readonly AppDbContext _context;

        public AdminIngredientRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Ingredient>> GetAllIngredientsAsync()
        {
            return await _context.Ingredients
                .OrderByDescending(i => i.IngredientId)
                .ToListAsync();
        }

        public async Task<Ingredient> GetIngredientByIdAsync(int id)
        {
            return await _context.Ingredients.FindAsync(id);
        }

        public async Task CreateIngredientAsync(Ingredient ingredient)
        {
            _context.Ingredients.Add(ingredient);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateIngredientAsync(Ingredient ingredient)
        {
            // Entity đã được track bởi DbContext qua FindAsync, chỉ cần SaveChanges.
            await _context.SaveChangesAsync();
        }

        public async Task ToggleIngredientStatusAsync(int id)
        {
            var ingredient = await _context.Ingredients.FindAsync(id);
            if (ingredient != null)
            {
                ingredient.Active = !ingredient.Active;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> IsIngredientCodeExistsAsync(string code, int? excludeId = null)
        {
            var query = _context.Ingredients.Where(i => i.Code.ToLower() == code.ToLower());
            if (excludeId.HasValue)
                query = query.Where(i => i.IngredientId != excludeId.Value);
            return await query.AnyAsync();
        }

        public async Task<bool> IsIngredientNameExistsAsync(string name, int? excludeId = null)
        {
            var query = _context.Ingredients.Where(i => i.Name.ToLower() == name.ToLower());
            if (excludeId.HasValue)
                query = query.Where(i => i.IngredientId != excludeId.Value);
            return await query.AnyAsync();
        }
    }
}
