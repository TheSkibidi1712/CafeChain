using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
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
        public async Task<List<Store>> GetStoresAsync()
            => await _context.Stores.AsNoTracking().ToListAsync();

        public async Task<List<Staff>> GetStaffsAsync()
            => await _context.Staffs.AsNoTracking().ToListAsync();

        public async Task<int?> GetStoreIdByStaffAsync(int staffId)
        {
            return await _context.Staffs
                .Where(x => x.StaffId == staffId)
                .Select(x => x.StoreId)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Supplier>> GetSuppliersAsync()
            => await _context.Suppliers.AsNoTracking().ToListAsync();

        public async Task<List<Ingredient>> GetIngredientsAsync()
            => await _context.Ingredients.AsNoTracking().ToListAsync();

        public async Task<List<Unit>> GetUnitsAsync()
            => await _context.Units.AsNoTracking().ToListAsync();


        // ======================== GET PAGED ========================
        public async Task<(List<InventoryDocument>, int)> GetPagedAsync(InventoryDocumentFilterDTO f)
        {
            var query = _context.InventoryDocuments
                .AsNoTracking()
                .Include(x => x.Store)
                .Include(x => x.Staff)
                .Include(x => x.Supplier)
                .Include(x => x.Details)
                    .ThenInclude(d => d.Ingredient)
                .Include(x => x.Details)
                    .ThenInclude(d => d.Unit)
                .AsQueryable();

            if (!string.IsNullOrEmpty(f.Keyword))
                query = query.Where(x => x.Code.Contains(f.Keyword));

            if (f.StoreId.HasValue)
                query = query.Where(x => x.StoreId == f.StoreId);

            if (f.Type.HasValue)
                query = query.Where(x => x.Type == f.Type);

            if (f.FromDate.HasValue)
                query = query.Where(x => x.DocumentDate >= f.FromDate);

            if (f.ToDate.HasValue)
                query = query.Where(x => x.DocumentDate <= f.ToDate);

            var total = await query.CountAsync();

            var data = await query
                .OrderByDescending(x => x.DocumentDate)
                .Skip((f.Page - 1) * f.PageSize)
                .Take(f.PageSize)
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
                .Include(x => x.Details)
                    .ThenInclude(d => d.Ingredient)
                .Include(x => x.Details)
                    .ThenInclude(d => d.Unit)
                .FirstOrDefaultAsync(x => x.InventoryDocumentId == id);
        }


        // ======================== ADD DOCUMENT ========================
        public async Task AddAsync(InventoryDocument document)
        {
            await _context.InventoryDocuments.AddAsync(document);
        }


        // ======================== INGREDIENT ========================
        public async Task<Ingredient?> GetIngredientAsync(int ingredientId)
        {
            return await _context.Ingredients
                .FirstOrDefaultAsync(x => x.IngredientId == ingredientId);
        }

        public async Task<List<IngredientSupplier>> GetIngredientSuppliersAsync(int ingredientId, int? supplierId)
        {
            var query = _context.IngredientSuppliers
                .Include(x => x.Unit)
                .Where(x => x.IngredientId == ingredientId);

            if (supplierId.HasValue)
                query = query.Where(x => x.SupplierId == supplierId);

            return await query.AsNoTracking().ToListAsync();
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
        public async Task<List<StoreInventory>> GetStoreInventoriesAsync(int storeId)
        {
            return await _context.StoreInventories
                .Include(x => x.Ingredient)
                .Where(x => x.StoreId == storeId && x.AvailableQty > 0)
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
