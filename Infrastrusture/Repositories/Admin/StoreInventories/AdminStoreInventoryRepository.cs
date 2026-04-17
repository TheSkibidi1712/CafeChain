using CafeChain.Application.DTOs.Admin.StoreInventories;
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
        public async Task<(List<InventoryDTO>, int total)> GetPagedAsync(int storeId, string? search, int page, int pageSize)
        {
            var query = _context.StoreInventories
                .Where(x => x.StoreId == storeId);

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(x => x.Ingredient.Name.Contains(search));
            }

            var total = await query.CountAsync();

            var data = await query
                .Select(x => new InventoryDTO
                {
                    StoreInventoryId = x.StoreInventoryId,
                    IngredientName = x.Ingredient.Name,
                    AvailableQty = x.AvailableQty,
                    ReservedQty = x.ReservedQty,
                    LastUpdated = x.LastUpdated,
                    UnitCode = x.Ingredient.BaseUnit.UnitCode,

                    // 🔥 GIÁ GẦN NHẤT (CÁCH 3)
                    LastUnitPrice = _context.InventoryDocumentDetails
                        .Where(d => d.IngredientId == x.IngredientId
                                 && d.InventoryDocument.Status == Models.Enums.Inventory.InventoryDocumentStatus.CONFIRMED)
                        .Select(d => new
                        {
                            d.UnitPrice,
                            d.InventoryDocument.DocumentDate
                        })
                        .OrderByDescending(d => d.DocumentDate)
                        .Select(d => d.UnitPrice)
                        .FirstOrDefault(),

                    // 🔥 NCC GẦN NHẤT (CÁCH 3)
                    LastSupplierName = _context.InventoryDocumentDetails
                        .Where(d => d.IngredientId == x.IngredientId
                                 && d.InventoryDocument.Status == Models.Enums.Inventory.InventoryDocumentStatus.CONFIRMED)
                        .Select(d => new
                        {
                            SupplierName = d.InventoryDocument.Supplier.Name,
                            d.InventoryDocument.DocumentDate
                        })
                        .OrderByDescending(d => d.DocumentDate)
                        .Select(d => d.SupplierName)
                        .FirstOrDefault()
                })
                .OrderBy(x => x.IngredientName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (data, total);
        }


        public async Task<(List<InventoryTransaction>, int total)> GetTransactionsByInventoryIdAsync(int storeInventoryId, int page, int pageSize)
        {
            var query = _context.InventoryTransactions.AsNoTracking()
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
            var query = _context.InventoryTransactions.AsNoTracking()
                .Include(x => x.InventoryDocument)
                    .ThenInclude(d => d.Details)
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
