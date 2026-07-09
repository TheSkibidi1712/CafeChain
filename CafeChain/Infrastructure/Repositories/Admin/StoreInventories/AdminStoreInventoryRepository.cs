using CafeChain.Application.DTOs.Admin.StoreInventories;
using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.StoreInventories;
using CafeChain.Models.Staffs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
        // STORE SCOPE
        // =====================================================

        public async Task<List<InventoryStoreDTO>> GetAccessibleStoresByAccountIdAsync(
            int accountId)
        {
            var staff = await _context.Staffs
                .AsNoTracking()
                .Include(x => x.StaffScopes)
                    .ThenInclude(x => x.ScopeType)
                .FirstOrDefaultAsync(x =>
                    x.AccountId == accountId &&
                    x.Active);

            if (staff == null)
                return new List<InventoryStoreDTO>();

            var query = BuildStoreScopeQuery(staff);

            return await query
                .OrderBy(x => x.Name)
                .Select(x => new InventoryStoreDTO
                {
                    StoreId = x.StoreId,
                    StoreName = x.Name
                })
                .ToListAsync();
        }

        // =====================================================
        // INVENTORY
        // =====================================================

        public async Task<(List<InventoryDTO> data, int total)> GetPagedAsync(
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
                var keyword = search.Trim();

                query = query.Where(x =>
                    x.IngredientId.HasValue &&
                    x.Ingredient.Name.Contains(keyword));
            }

            var total = await query.CountAsync();

            var data = await query
                .Select(x => new InventoryDTO
                {
                    StoreInventoryId = x.StoreInventoryId,

                    StoreId = x.StoreId,
                    StoreName = x.Store.Name,

                    IngredientName = x.IngredientId.HasValue
                        ? x.Ingredient.Name
                        : x.RecipeId.HasValue
                            ? "Công thức #" + x.RecipeId.Value
                            : "Không xác định",

                    AvailableQty = x.AvailableQty,
                    ReservedQty = x.ReservedQty,
                    MaxNegativeQty = x.MaxNegativeQty,
                    LastUpdated = x.LastUpdated,

                    UnitCode = x.IngredientId.HasValue && x.Ingredient.BaseUnit != null
                        ? x.Ingredient.BaseUnit.UnitCode
                        : string.Empty,

                    // Không phụ thuộc DocumentDate nữa. Lấy theo transaction thật sự mới nhất.
                    LastUnitPrice = _context.InventoryTransactions
                        .Where(t =>
                            t.StoreInventoryId == x.StoreInventoryId &&
                            t.UnitCost.HasValue)
                        .OrderByDescending(t => t.CreatedAt)
                        .Select(t => t.UnitCost)
                        .FirstOrDefault(),

                    LastSupplierName = _context.InventoryTransactions
                        .Where(t =>
                            t.StoreInventoryId == x.StoreInventoryId &&
                            t.InventoryDocument != null &&
                            t.InventoryDocument.Supplier != null)
                        .OrderByDescending(t => t.CreatedAt)
                        .Select(t => t.InventoryDocument.Supplier!.Name)
                        .FirstOrDefault()
                })
                .OrderBy(x => x.StoreName)
                .ThenBy(x => x.IngredientName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (data, total);
        }

        // =====================================================
        // TRANSACTIONS
        // =====================================================

        public async Task<(List<InventoryTransactionDTO> data, int total)> GetTransactionsByStoreIdsAsync(
            List<int> storeIds,
            int storeId,
            int page,
            int pageSize)
        {
            if (storeIds == null || !storeIds.Any())
                return (new List<InventoryTransactionDTO>(), 0);

            var query = _context.InventoryTransactions
                .AsNoTracking()
                .Where(x =>
                    x.StoreInventory != null &&
                    storeIds.Contains(x.StoreInventory.StoreId));

            if (storeId > 0)
            {
                query = query.Where(x => x.StoreInventory.StoreId == storeId);
            }

            return await GetTransactionPageAsync(
                query,
                page,
                pageSize);
        }

        public async Task<(List<InventoryTransactionDTO> data, int total)> GetTransactionsByStoreInventoryIdAsync(
            List<int> storeIds,
            int storeInventoryId,
            int page,
            int pageSize)
        {
            if (storeIds == null || !storeIds.Any() || storeInventoryId <= 0)
                return (new List<InventoryTransactionDTO>(), 0);

            var query = _context.InventoryTransactions
                .AsNoTracking()
                .Where(x =>
                    x.StoreInventoryId == storeInventoryId &&
                    x.StoreInventory != null &&
                    storeIds.Contains(x.StoreInventory.StoreId));

            return await GetTransactionPageAsync(
                query,
                page,
                pageSize);
        }

        // =====================================================
        // UNIT OF WORK
        // =====================================================

        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            return await _context.Database.BeginTransactionAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        // =====================================================
        // PRIVATE - STORE SCOPE
        // =====================================================

        private IQueryable<CafeChain.Models.Stores.Store> BuildStoreScopeQuery(
            Staff staff)
        {
            var activeStores = _context.Stores
                .AsNoTracking()
                .Where(x => x.Active);

            var scopes = staff.StaffScopes?
                .Where(x => x.ScopeRefId > 0)
                .ToList()
                ?? new List<StaffScope>();

            if (HasScope(scopes, "COUNTRY", 1))
                return activeStores;

            var storeScopeIds = GetScopeRefIds(scopes, "STORE", 4);
            var provinceScopeIds = GetScopeRefIds(scopes, "PROVINCE", 2);
            var wardScopeIds = GetScopeRefIds(scopes, "WARD", 3);

            if (staff.StoreId > 0)
            {
                storeScopeIds.Add(staff.StoreId);
            }

            storeScopeIds = storeScopeIds.Distinct().ToList();
            provinceScopeIds = provinceScopeIds.Distinct().ToList();
            wardScopeIds = wardScopeIds.Distinct().ToList();

            if (!storeScopeIds.Any() &&
                !provinceScopeIds.Any() &&
                !wardScopeIds.Any())
            {
                return activeStores.Where(x => false);
            }

            return activeStores.Where(x =>
                storeScopeIds.Contains(x.StoreId) ||
                (x.ProvinceId.HasValue && provinceScopeIds.Contains(x.ProvinceId.Value)) ||
                (x.WardId.HasValue && wardScopeIds.Contains(x.WardId.Value)));
        }

        private static List<int> GetScopeRefIds(
            IEnumerable<StaffScope> scopes,
            string code,
            int scopeTypeId)
        {
            return scopes
                .Where(x => IsScope(x, code, scopeTypeId))
                .Select(x => x.ScopeRefId)
                .Distinct()
                .ToList();
        }

        private static bool HasScope(
            IEnumerable<StaffScope> scopes,
            string code,
            int scopeTypeId)
        {
            return scopes.Any(x => IsScope(x, code, scopeTypeId));
        }

        private static bool IsScope(
            StaffScope scope,
            string code,
            int scopeTypeId)
        {
            return scope.ScopeTypeId == scopeTypeId ||
                   string.Equals(
                       scope.ScopeType?.Code,
                       code,
                       StringComparison.OrdinalIgnoreCase);
        }

        // =====================================================
        // PRIVATE - TRANSACTION PROJECTION
        // =====================================================

        private async Task<(List<InventoryTransactionDTO> data, int total)> GetTransactionPageAsync(
            IQueryable<CafeChain.Models.Inventories.Transactions.InventoryTransaction> query,
            int page,
            int pageSize)
        {
            var total = await query.CountAsync();

            var rows = await query
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.InventoryTransactionId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.InventoryTransactionId,
                    x.StoreInventoryId,

                    StoreId = x.StoreInventory.StoreId,
                    StoreName = x.StoreInventory.Store.Name,

                    IngredientId = x.StoreInventory.IngredientId,
                    RecipeId = x.StoreInventory.RecipeId,
                    IngredientName = x.StoreInventory.IngredientId.HasValue
                        ? x.StoreInventory.Ingredient.Name
                        : null,
                    UnitCode = x.StoreInventory.IngredientId.HasValue &&
                               x.StoreInventory.Ingredient.BaseUnit != null
                        ? x.StoreInventory.Ingredient.BaseUnit.UnitCode
                        : string.Empty,

                    x.Type,
                    x.StockStatus,
                    x.Quantity,
                    x.BeforeQty,
                    x.AfterQty,
                    x.UnitCost,
                    x.TotalCost,
                    x.InventoryDocumentId,
                    x.InventoryTransferId,
                    x.ReferenceOrderId,
                    x.CreatedAt
                })
                .ToListAsync();

            var data = rows
                .Select(x => new InventoryTransactionDTO
                {
                    InventoryTransactionId = x.InventoryTransactionId,
                    StoreInventoryId = x.StoreInventoryId,

                    StoreId = x.StoreId,
                    StoreName = x.StoreName,

                    IngredientName = !string.IsNullOrWhiteSpace(x.IngredientName)
                        ? x.IngredientName
                        : x.RecipeId.HasValue
                            ? $"Công thức #{x.RecipeId.Value}"
                            : "Không xác định",

                    TypeName = x.Type.ToString(),
                    StockStatusName = x.StockStatus.ToString(),

                    Quantity = x.Quantity,
                    BeforeQty = x.BeforeQty,
                    AfterQty = x.AfterQty,

                    UnitPrice = x.UnitCost,
                    TotalAmount = x.TotalCost,

                    InventoryDocumentId = x.InventoryDocumentId,
                    InventoryTransferId = x.InventoryTransferId,
                    ReferenceOrderId = x.ReferenceOrderId,
                    ReferenceType = BuildReferenceType(
                        x.InventoryDocumentId,
                        x.InventoryTransferId,
                        x.ReferenceOrderId),

                    CreatedAt = x.CreatedAt,
                    UnitCode = x.UnitCode
                })
                .ToList();

            return (data, total);
        }

        private static string BuildReferenceType(
            int? inventoryDocumentId,
            int? inventoryTransferId,
            int? referenceOrderId)
        {
            if (referenceOrderId.HasValue)
                return $"POS / Đơn hàng #{referenceOrderId.Value}";

            if (inventoryTransferId.HasValue)
                return $"Chuyển kho #{inventoryTransferId.Value}";

            if (inventoryDocumentId.HasValue)
                return $"Phiếu kho #{inventoryDocumentId.Value}";

            return "Giao dịch kho";
        }
    }
}
