using CafeChain.Application.DTOs.Admin.StoreInventories;
using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.StoreInventories;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Staffs;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastrusture.Repositories.Admin.StoreInventories
{
    public class AdminStoreInventoryRepository : IAdminStoreInventoryRepository
    {
        private readonly AppDbContext _context;

        public AdminStoreInventoryRepository(AppDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // INVENTORY
        // =====================================================

        public async Task<(List<InventoryDTO>, int total)> GetPagedAsync(
            List<int> storeIds,
            int storeId,
            string? search,
            int page,
            int pageSize)
        {
            if (storeIds == null || !storeIds.Any())
                return (new List<InventoryDTO>(), 0);

            var query = _context.StoreInventories
                .AsNoTracking()
                .Where(x => storeIds.Contains(x.StoreId));

            if (storeId > 0)
            {
                query = query.Where(x => x.StoreId == storeId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    x.Ingredient.Name.Contains(search));
            }

            var total = await query.CountAsync();

            var data = await query
                .Select(x => new InventoryDTO
                {
                    StoreInventoryId = x.StoreInventoryId,

                    StoreId = x.StoreId,
                    StoreName = x.Store.Name,

                    IngredientName = x.Ingredient.Name,

                    AvailableQty = x.AvailableQty,
                    ReservedQty = x.ReservedQty,
                    LastUpdated = x.LastUpdated,

                    UnitCode = x.Ingredient.BaseUnit.UnitCode,

                    LastUnitPrice = _context.InventoryDocumentDetails
                        .Where(d =>
                            d.IngredientId == x.IngredientId &&
                            d.InventoryDocument.Status == InventoryDocumentStatus.CONFIRMED)
                        .OrderByDescending(d => d.InventoryDocument.DocumentDate)
                        .Select(d => d.UnitPrice)
                        .FirstOrDefault(),

                    LastSupplierName = _context.InventoryDocumentDetails
                        .Where(d =>
                            d.IngredientId == x.IngredientId &&
                            d.InventoryDocument.Status == InventoryDocumentStatus.CONFIRMED)
                        .OrderByDescending(d => d.InventoryDocument.DocumentDate)
                        .Select(d => d.InventoryDocument.Supplier != null
                            ? d.InventoryDocument.Supplier.Name
                            : null)
                        .FirstOrDefault()
                })
                .OrderBy(x => x.IngredientName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (data, total);
        }

        // =====================================================
        // ALL TRANSACTIONS
        // =====================================================

        public async Task<(List<InventoryTransaction>, int total)> GetTransactionsByStoreIdsAsync(
            List<int> storeIds,
            int storeId,
            int page,
            int pageSize)
        {
            if (storeIds == null || !storeIds.Any())
                return (new List<InventoryTransaction>(), 0);

            var query = _context.InventoryTransactions
                .AsNoTracking()
                .Include(x => x.InventoryDocument)
                    .ThenInclude(d => d.Details)
                .Include(x => x.StoreInventory)
                    .ThenInclude(si => si.Ingredient)
                        .ThenInclude(i => i.BaseUnit)
                .Include(x => x.StoreInventory)
                    .ThenInclude(si => si.Store)
                .Where(x =>
                    x.StoreInventory != null &&
                    storeIds.Contains(x.StoreInventory.StoreId));

            if (storeId > 0)
            {
                query = query.Where(x =>
                    x.StoreInventory.StoreId == storeId);
            }

            var total = await query.CountAsync();

            var data = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (data, total);
        }

        // =====================================================
        // LEGACY
        // =====================================================

        public async Task<(List<InventoryTransaction>, int total)> GetTransactionsByStoreIdAsync(
            int storeId,
            int page,
            int pageSize)
        {
            return await GetTransactionsByStoreIdsAsync(
                new List<int> { storeId },
                storeId,
                page,
                pageSize);
        }

        // =====================================================
        // STAFF
        // =====================================================

        public async Task<Staff?> GetStaffByAccountIdAsync(
            int accountId)
        {
            return await _context.Staffs
                .AsNoTracking()
                .Include(x => x.StaffScopes)
                    .ThenInclude(x => x.ScopeType)
                .FirstOrDefaultAsync(x =>
                    x.AccountId == accountId &&
                    x.Active);
        }

        // =====================================================
        // SAVE
        // =====================================================

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}