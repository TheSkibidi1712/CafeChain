using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace CafeChain.Infrastrusture.Repositories.Admin.InventoryDocuments
{
    public class AdminInventoryDocumentRepository : IAdminInventoryDocumentRepository
    {
        private readonly AppDbContext _context;

        public AdminInventoryDocumentRepository(AppDbContext context)
        {
            _context = context;
        }

        // ======================== LOOKUP ========================
        public async Task<List<Store>> GetStoresByStaffAsync(int staffId)
        {
            var storeIds = await _context.StaffScopes
                .Where(x => x.StaffId == staffId && x.ScopeTypeId == 4) // STORE
                .Select(x => x.ScopeRefId)
                .ToListAsync();

            return await _context.Stores
                .Where(x => storeIds.Contains(x.StoreId))
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<bool> CheckStaffHasStoreAsync(int staffId, int storeId)
        {
            return await _context.StaffScopes
                .AnyAsync(x =>
                    x.StaffId == staffId &&
                    x.ScopeTypeId == 4 &&
                    x.ScopeRefId == storeId);
        }

        public async Task<List<Supplier>> GetSuppliersAsync()
            => await _context.Suppliers.AsNoTracking().ToListAsync();

        public async Task<Supplier?> GetSupplierByIdAsync(int id)
        {
            return await _context.Suppliers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.SupplierId == id);
        }

        // ======================== GET PAGED ========================
        public async Task<(List<InventoryDocument>, int)> GetPagedAsync(InventoryDocumentFilterDTO f)
        {
            var query = _context.InventoryDocuments
                .AsNoTracking()
                .Include(x => x.Store)
                .Include(x => x.Staff)
                .Include(x => x.Supplier)
                .Include(x => x.ExportTransfer)
                .Include(x => x.ImportTransfer)
                .Include(x => x.Details)
                    .ThenInclude(d => d.Ingredient)
                        .ThenInclude(i => i.BaseUnit)
                .Include(x => x.Details)
                    .ThenInclude(d => d.Unit)
                .AsQueryable();

            // ================= FILTER KEYWORD =================
            if (!string.IsNullOrEmpty(f.Keyword))
            {
                query = query.Where(x => x.Code.Contains(f.Keyword));
            }

            // ================= FILTER STORE =================
            if (f.StoreId.HasValue)
            {
                query = query.Where(x => x.StoreId == f.StoreId);
            }

            // ================= FILTER TYPE (QUAN TRỌNG) =================
            if (f.Type.HasValue)
            {
                query = query.Where(x => x.Type == f.Type);
            }

            // ================= FILTER DATE =================
            if (f.FromDate.HasValue)
            {
                query = query.Where(x => x.DocumentDate >= f.FromDate);
            }

            if (f.ToDate.HasValue)
            {
                query = query.Where(x => x.DocumentDate <= f.ToDate);
            }

            // ================= TOTAL =================
            var total = await query.CountAsync();

            // ================= PAGING =================
            var data = await query
                .OrderByDescending(x => x.DocumentDate)
                .Skip((f.Page - 1) * 10)
                .Take(10)
                .ToListAsync();

            return (data, total);
        }


        // ======================== GET DETAIL ========================
        public async Task<InventoryDocument?> GetDetailAsync(int id)
        {
            return await _context.InventoryDocuments
                .AsNoTracking()
                .Include(x => x.Store)
                .Include(x => x.Staff)
                .Include(x => x.Supplier)
                .Include(x => x.ExportTransfer)
                .Include(x => x.ImportTransfer)
                .Include(x => x.Details)
                    .ThenInclude(d => d.Ingredient)
                        .ThenInclude(i => i.BaseUnit)
                .Include(x => x.Details)
                    .ThenInclude(d => d.Unit)
                .FirstOrDefaultAsync(x => x.InventoryDocumentId == id);
        }


        // ======================== ADD DOCUMENT ========================
        public async Task AddAsync(InventoryDocument document)
        {
            await _context.InventoryDocuments.AddAsync(document);
        }

        public async Task AddDebtAsync(InventoryDebt debt)
        {
            await _context.InventoryDebts.AddAsync(debt);
        }

        public async Task<InventoryDebt?> GetDebtByDocumentIdAsync(int documentId)
        {
            return await _context.InventoryDebts
                .FirstOrDefaultAsync(x => x.InventoryDocumentId == documentId);
        }

        // ======================== UPDATE STOCK ========================
        public async Task UpdateStoreInventoryAsync(StoreInventory stock)
        {
            _context.StoreInventories.Update(stock);
            await Task.CompletedTask;
        }

        // ======================== INGREDIENT ========================
        public async Task<Ingredient?> GetIngredientAsync(int ingredientId)
        {
            return await _context.Ingredients
                .FirstOrDefaultAsync(x => x.IngredientId == ingredientId);
        }

        public async Task<IngredientSupplier?> GetIngredientSupplierAsync(int ingredientId, int supplierId)
        {
            return await _context.IngredientSuppliers
                .Include(x => x.Unit)
                .FirstOrDefaultAsync(x =>
                    x.IngredientId == ingredientId &&
                    x.SupplierId == supplierId);
        }

        public async Task<decimal?> GetLastPriceAsync(int storeId, int ingredientId)
        {
            return await _context.InventoryDocumentDetails
                .Where(d =>
                    d.IngredientId == ingredientId &&
                    d.UnitPrice != null &&
                    d.InventoryDocument.Type == InventoryDocumentType.IMPORT &&
                    d.InventoryDocument.StoreId == storeId &&
                    d.InventoryDocument.Status == InventoryDocumentStatus.CONFIRMED
                )
                .OrderByDescending(d => d.InventoryDocument.DocumentDate)
                .Select(d => d.UnitPrice)
                .FirstOrDefaultAsync();
        }

        public async Task<List<IngredientSupplier>> GetIngredientSuppliersBySupplierAsync(int supplierId)
        {
            return await _context.IngredientSuppliers
                .AsNoTracking()
                .Include(x => x.Ingredient)
                .Include(x => x.Unit)
                .Where(x => x.SupplierId == supplierId)
                .ToListAsync();
        }

        // ======================== STORE INVENTORY ========================
        public async Task<StoreInventory?> GetStoreInventoryAsync(int storeId, int ingredientId)
        {
            return await _context.StoreInventories
                .FirstOrDefaultAsync(x => x.StoreId == storeId && x.IngredientId == ingredientId);
        }

        public async Task AddStoreInventoryAsync(StoreInventory stock)
        {
            await _context.StoreInventories.AddAsync(stock);
        }


        // ======================== UNIT ========================
        public async Task<UnitConversion?> GetConversionAsync(int ingredientId, int fromUnitId, int toUnitId)
        {
            return await _context.UnitConversions
                .FirstOrDefaultAsync(x =>
                    x.IngredientId == ingredientId &&
                    x.FromUnitId == fromUnitId &&
                    x.ToUnitId == toUnitId);
        }

        public async Task<List<Unit>> GetUnitsByIngredientAsync(int ingredientId)
        {
            var units = await _context.UnitConversions
                .Where(x => x.IngredientId == ingredientId)
                .Select(x => x.FromUnit)
                .Union(
                    _context.UnitConversions
                        .Where(x => x.IngredientId == ingredientId)
                        .Select(x => x.ToUnit)
                )
                .Distinct()
                .ToListAsync();

            var baseUnit = await _context.Ingredients
                .Where(x => x.IngredientId == ingredientId)
                .Select(x => x.BaseUnit)
                .FirstOrDefaultAsync();

            if (baseUnit != null && !units.Any(u => u.UnitId == baseUnit.UnitId))
            {
                units.Add(baseUnit);
            }

            return units;
        }

        // ======================== GET STORE INVENTORIES ========================
        public async Task<List<StoreInventory>> GetStoreInventoriesAsync(int storeId, bool onlyAvailable = false)
        {
            var query = _context.StoreInventories
                .Include(x => x.Ingredient)
                    .ThenInclude(i => i.BaseUnit)
                .Where(x => x.StoreId == storeId);

            if (onlyAvailable)
                query = query.Where(x => x.AvailableQty > 0);

            return await query
                .AsNoTracking()
                .ToListAsync();
        }


        // ======================== TRANSACTION ========================
        public async Task AddTransactionAsync(InventoryTransaction transaction)
        {
            await _context.InventoryTransactions.AddAsync(transaction);
        }


        // ======================== SAVE ========================
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }


        // ======================== DB TRANSACTION ========================
        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            return await _context.Database.BeginTransactionAsync();
        }

    }
}
