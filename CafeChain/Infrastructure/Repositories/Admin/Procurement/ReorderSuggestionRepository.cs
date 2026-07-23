using CafeChain.Application.Constants;
using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Admin.Procurement;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastructure.Repositories.Admin.Procurement;

public sealed class ReorderSuggestionRepository : IReorderSuggestionRepository
{
    private readonly AppDbContext _context;

    public ReorderSuggestionRepository(AppDbContext context) => _context = context;

    public Task<ReorderStoreRow?> GetStoreAsync(int storeId) =>
        _context.Stores.AsNoTracking()
            .Where(x => x.StoreId == storeId)
            .Select(x => new ReorderStoreRow(x.StoreId, x.Name ?? $"Cửa hàng #{x.StoreId}"))
            .FirstOrDefaultAsync();

    public Task<List<StoreInventory>> GetInventoriesAsync(int storeId) =>
        _context.StoreInventories.AsNoTracking()
            .Include(x => x.Ingredient)
                .ThenInclude(x => x.BaseUnit)
            .Where(x => x.StoreId == storeId && x.IngredientId.HasValue && x.Ingredient.Active)
            .OrderBy(x => x.Ingredient.Name)
            .ThenBy(x => x.IngredientId)
            .ToListAsync();

    public async Task<IReadOnlyDictionary<int, ReorderUsageRow>> GetUsageAsync(
        int storeId,
        DateTime fromUtc)
    {
        var rows = await _context.InventoryTransactions.AsNoTracking()
            .Where(x => x.CreatedAt >= fromUtc
                && x.StoreInventory.StoreId == storeId
                && x.StoreInventory.IngredientId.HasValue
                && (x.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION
                    || x.Type == InventoryTransactionTypeEnum.PRODUCTION_OUT))
            .Select(x => new { IngredientId = x.StoreInventory.IngredientId!.Value, x.Quantity })
            .ToListAsync();
        return rows.GroupBy(x => x.IngredientId)
            .ToDictionary(
                x => x.Key,
                x => new ReorderUsageRow(x.Sum(y => y.Quantity), x.Count()));
    }

    public Task<List<IngredientSupplier>> GetOffersAsync(
        int storeId,
        IReadOnlyCollection<int> ingredientIds) =>
        _context.IngredientSuppliers.AsNoTracking()
            .Include(x => x.Supplier)
                .ThenInclude(x => x.SupplierStores)
            .Where(x => ingredientIds.Contains(x.IngredientId)
                && x.Active
                && x.Supplier.Active
                && x.Supplier.SupplierStores.Any(ss => ss.StoreId == storeId && ss.Active))
            .ToListAsync();

    public async Task<IReadOnlyDictionary<int, int>> GetActiveRestockRequestsAsync(int storeId) =>
        await _context.RestockRequests.AsNoTracking()
            .Where(x => x.StoreId == storeId
                && x.IngredientId.HasValue
                && RestockRequestStatuses.ActiveValues.Contains(x.Status))
            .GroupBy(x => x.IngredientId!.Value)
            .Select(x => new { IngredientId = x.Key, RequestId = x.Min(y => y.RestockRequestId) })
            .ToDictionaryAsync(x => x.IngredientId, x => x.RequestId);

    public async Task<IReadOnlyDictionary<int, decimal>> GetActivePurchaseAdviceQuantitiesAsync(int storeId)
    {
        var activeStatuses = PurchaseAdviceStatuses.ActiveReservationStatuses.ToArray();
        var rows = await _context.PurchaseAdviceLines.AsNoTracking()
            .Where(x => x.PurchaseAdvice.StoreId == storeId
                && x.IsActiveReservation
                && activeStatuses.Contains(x.PurchaseAdvice.Status))
            .Select(x => new
            {
                x.IngredientId,
                Remaining = Math.Max(0m, x.RequestedPurchaseBaseQuantity - x.AllocatedToPoBaseQuantity - x.ClosedBaseQuantity)
            })
            .ToListAsync();
        return rows.GroupBy(x => x.IngredientId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Remaining));
    }

    public Task<bool> IsActiveStaffAtStoreAsync(int staffId, int storeId) =>
        _context.Staffs.AsNoTracking()
            .AnyAsync(x => x.StaffId == staffId && x.Active && x.StoreId == storeId);

    public async Task<IReadOnlyList<int>> GetActiveStoreIdsAsync() =>
        await _context.Stores.AsNoTracking()
            .Where(x => x.Active)
            .OrderBy(x => x.StoreId)
            .Select(x => x.StoreId)
            .ToListAsync();
}
