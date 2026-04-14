using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.StoreInventories;
using CafeChain.Models.Inventories;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
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

        // ================= INVENTORY =================

        public async Task<(List<StoreInventory>, int total)> GetPagedAsync(int storeId, string? search, int page, int pageSize)
        {
            var query = _context.StoreInventories
                .Include(x => x.Ingredient)
                    .ThenInclude(i => i.BaseUnit)
                .Where(x => x.StoreId == storeId);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(x => x.Ingredient.Name.Contains(search));
            }

            var total = await query.CountAsync();

            var data = await query
                .OrderBy(x => x.Ingredient.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (data, total);
        }
        public async Task<(List<InventoryTransaction>, int total)> GetTransactionsByInventoryIdAsync(int storeInventoryId, int page, int pageSize)
        {
            var query = _context.InventoryTransactions
                .Include(x => x.TransactionType)
                .Include(x => x.StoreInventory)
                    .ThenInclude(si => si.Ingredient)
                        .ThenInclude(i => i.BaseUnit)
                .Where(x => x.StoreInventoryId == storeInventoryId);

            var total = await query.CountAsync();

            var data = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (data, total);
        }

        // ================= ALL TRANSACTIONS BY STAFF =================
        public async Task<(List<InventoryTransaction>, int total)> GetTransactionsByStoreIdAsync(int storeId, int page, int pageSize)
        {
            var query = _context.InventoryTransactions
                .Include(x => x.TransactionType)
                .Include(x => x.StoreInventory)
                    .ThenInclude(si => si.Ingredient)
                        .ThenInclude(i => i.BaseUnit)
                .Where(x => x.StoreInventory.StoreId == storeId);

            var total = await query.CountAsync();

            var data = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (data, total);
        }

        // ================= STAFF =================

        public async Task<Staff?> GetStaffByAccountIdAsync(int accountId)
        {
            return await _context.Staffs
                .Include(x => x.StaffScopes)
                    .ThenInclude(s => s.ScopeType)
                .FirstOrDefaultAsync(x => x.AccountId == accountId && x.Active);
        }

        // ================= SAVE =================

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
