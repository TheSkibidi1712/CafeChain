using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Analytics;
using CafeChain.Models.Inventories.Suppliers;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastructure.Repositories.Analytics;

public sealed class SupplierIntelligenceRepository : ISupplierIntelligenceRepository
{
    private readonly AppDbContext _context;
    public SupplierIntelligenceRepository(AppDbContext context) => _context = context;
    public Task<List<IngredientSupplier>> GetOffersAsync(int storeId, int ingredientId, CancellationToken ct) =>
        _context.IngredientSuppliers.AsNoTracking().Include(x => x.Supplier).Include(x => x.Ingredient)
            .Where(x => x.IngredientId == ingredientId && x.Active && x.Supplier.Active
                && x.Supplier.SupplierStores.Any(s => s.StoreId == storeId && s.Active))
            .OrderByDescending(x => x.IsPrimary).ThenBy(x => x.SupplierId).ToListAsync(ct);
}
