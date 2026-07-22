using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.Ingredients;
using CafeChain.Models.Inventories.Ingredients;
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
        public async Task<(List<Ingredient> Items, int Total)> GetPagedAsync(string? search, bool? status, int page, int pageSize)
        {
            var query = _context.Ingredients
                .Include(x => x.BaseUnit)
                .AsQueryable();

            // SEARCH
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();

                query = query.Where(x =>
                    x.Code.ToLower().Contains(search) ||
                    x.Name.ToLower().Contains(search));
            }

            // FILTER STATUS
            if (status.HasValue)
            {
                query = query.Where(x => x.Active == status.Value);
            }

            var total = await query.CountAsync();

            var data = await query
                .OrderBy(x => x.IngredientId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (data, total);
        }

        // ================= GET BY ID =================
        public async Task<Ingredient?> GetByIdAsync(int id)
        {
            return await _context.Ingredients
                .Include(x => x.BaseUnit)
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

        public Task<bool> IsActiveUnitAsync(int unitId) =>
            _context.Units.AnyAsync(x => x.UnitId == unitId && x.Active);

        public async Task<bool> HasBaseUnitDependenciesAsync(int ingredientId)
        {
            return await _context.StoreInventories.AnyAsync(x => x.IngredientId == ingredientId)
                || await _context.InventoryTransactions.AnyAsync(x => x.StoreInventory.IngredientId == ingredientId)
                || await _context.RecipeDetails.AnyAsync(x => x.IngredientId == ingredientId)
                || await _context.IngredientSuppliers.AnyAsync(x => x.IngredientId == ingredientId)
                || await _context.InventoryDocumentDetails.AnyAsync(x => x.IngredientId == ingredientId)
                || await _context.InventoryTransferDetails.AnyAsync(x => x.IngredientId == ingredientId)
                || await _context.RestockRequests.AnyAsync(x => x.IngredientId == ingredientId)
                || await _context.PurchaseAdviceLines.AnyAsync(x => x.IngredientId == ingredientId)
                || await _context.PurchaseOrderLines.AnyAsync(x => x.IngredientId == ingredientId)
                || await _context.BranchReceiptLines.AnyAsync(x => x.IngredientId == ingredientId)
                || await _context.StockTakeDetails.AnyAsync(x => x.IngredientId == ingredientId)
                || await _context.UnitConversions.AnyAsync(x => x.IngredientId == ingredientId);
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

    }
}
