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

        // ================= GET ALL =================
        public async Task<List<Ingredient>> GetAllAsync(string? search, bool? status)
        {
            var query = _context.Ingredients
                .Include(x => x.BaseUnit)
                .AsQueryable();

            // 🔍 SEARCH (code + name)
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();

                query = query.Where(x =>
                    x.Code.ToLower().Contains(search) ||
                    x.Name.ToLower().Contains(search));
            }

            // 🔥 FILTER STATUS
            if (status.HasValue)
            {
                query = query.Where(x => x.Active == status.Value);
            }

            return await query
                .OrderBy(x => x.IngredientId)
                .ToListAsync();
        }

        // ================= GET BY ID =================
        public async Task<Ingredient?> GetByIdAsync(int id)
        {
            return await _context.Ingredients
                .Include(x => x.BaseUnit)
                .Include(x => x.UnitConversions)
                    .ThenInclude(c => c.FromUnit)
                .Include(x => x.UnitConversions)
                    .ThenInclude(c => c.ToUnit)
                .FirstOrDefaultAsync(x => x.IngredientId == id);
        }

        // ================= CREATE =================
        public async Task CreateAsync(Ingredient ingredient)
        {
            await _context.Ingredients.AddAsync(ingredient);
        }

        // ================= UPDATE =================
        public async Task UpdateAsync(Ingredient ingredient)
        {
            // Nếu entity chưa track → attach
            var tracked = _context.Ingredients.Local
                .FirstOrDefault(x => x.IngredientId == ingredient.IngredientId);

            if (tracked == null)
            {
                _context.Ingredients.Attach(ingredient);
                _context.Entry(ingredient).State = EntityState.Modified;
            }

        }

        // ================= CHECK CODE =================
        public async Task<bool> IsCodeExists(string code, int? excludeId = null)
        {
            var query = _context.Ingredients
                .Where(x => x.Code.ToLower() == code.ToLower());

            if (excludeId.HasValue)
                query = query.Where(x => x.IngredientId != excludeId.Value);

            return await query.AnyAsync();
        }

        // ================= CHECK NAME =================
        public async Task<bool> IsNameExists(string name, int? excludeId = null)
        {
            var query = _context.Ingredients
                .Where(x => x.Name.ToLower() == name.ToLower());

            if (excludeId.HasValue)
                query = query.Where(x => x.IngredientId != excludeId.Value);

            return await query.AnyAsync();
        }

        // ================= TOGGLE STATUS =================
        public async Task ToggleStatus(int id)
        {
            var ingredient = await _context.Ingredients.FindAsync(id);

            if (ingredient == null)
                throw new Exception("Không tìm thấy nguyên liệu");

            ingredient.Active = !ingredient.Active;

        }

        // ================= SAVE CHANGES =================
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        // ================= GET ACTIVE UNITS =================
        public async Task<List<Unit>> GetActiveUnitsAsync()
        {
            return await _context.Units
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .ToListAsync();
        }


        // ================= ADD CONVERSIONS =================
        public async Task AddConversionsAsync(List<UnitConversion> conversions)
        {
            if (conversions == null || !conversions.Any())
                return;

            await _context.UnitConversions.AddRangeAsync(conversions);
        }

        // ================= REPLACE CONVERSIONS =================
        public async Task ReplaceConversionsAsync(int ingredientId, List<UnitConversion> conversions)
        {
            // 🔥 Xoá toàn bộ conversion cũ
            var old = _context.UnitConversions
                .Where(x => x.IngredientId == ingredientId);

            _context.UnitConversions.RemoveRange(old);

            // 🔥 Add mới
            if (conversions != null && conversions.Any())
            {
                foreach (var c in conversions)
                {
                    c.IngredientId = ingredientId;
                }

                await _context.UnitConversions.AddRangeAsync(conversions);
            }

        }
    }
}
