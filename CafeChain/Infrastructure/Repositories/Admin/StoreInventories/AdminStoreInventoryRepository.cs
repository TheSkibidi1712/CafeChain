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
            string inventoryType,
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

            if (string.Equals(
                inventoryType,
                InventoryCatalogTypes.PreparedItems,
                StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.PreparedItemId.HasValue || x.RecipeId.HasValue);
            }
            else
            {
                query = query.Where(x => x.IngredientId.HasValue);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim();

                query = query.Where(x =>
                    (x.IngredientId.HasValue && x.Ingredient.Name.Contains(keyword)) ||
                    (x.PreparedItemId.HasValue &&
                     (x.PreparedItem.Name.Contains(keyword) || x.PreparedItem.Code.Contains(keyword))) ||
                    (x.RecipeId.HasValue &&
                     (x.Recipe.Name.Contains(keyword) || x.Recipe.RecipeCode.Contains(keyword))));
            }

            var total = await query.CountAsync();

            var data = await query
                .Select(x => new InventoryDTO
                {
                    StoreInventoryId = x.StoreInventoryId,

                    StoreId = x.StoreId,
                    StoreName = x.Store.Name,

                    ItemCode = x.IngredientId.HasValue
                        ? x.Ingredient.Code
                        : x.PreparedItemId.HasValue
                            ? x.PreparedItem.Code
                            : x.RecipeId.HasValue
                                ? x.Recipe.RecipeCode
                                : string.Empty,
                    ItemType = x.IngredientId.HasValue
                        ? InventoryCatalogTypes.Ingredients
                        : InventoryCatalogTypes.PreparedItems,

                    IngredientName = x.IngredientId.HasValue
                        ? x.Ingredient.Name
                        : x.PreparedItemId.HasValue
                            ? x.PreparedItem.Name
                        : x.RecipeId.HasValue
                            ? x.Recipe.Name
                            : "Không xác định",

                    IdentityBadge = x.IngredientId.HasValue
                        ? string.Empty
                        : x.PreparedItemId.HasValue && x.RecipeId.HasValue
                            ? "BTP liên kết"
                            : x.PreparedItemId.HasValue
                                ? "BTP"
                                : x.RecipeId.HasValue
                                    ? "BTP legacy"
                                    : "Không xác định",
                    LegacyRecipeId = x.RecipeId,
                    PreparedItemId = x.PreparedItemId,
                    QuantitySemanticsStatus = x.IngredientId.HasValue
                        ? CafeChain.Application.DTOs.Inventories.QuantitySemanticsStatuses.NotApplicable
                        : x.RecipeId.HasValue
                            ? CafeChain.Application.DTOs.Inventories.QuantitySemanticsStatuses.Unknown
                            : CafeChain.Application.DTOs.Inventories.QuantitySemanticsStatuses.BaseUnitQuantityConfirmed,

                    AvailableQty = x.AvailableQty,
                    ReservedQty = x.ReservedQty,
                    MaxNegativeQty = x.MaxNegativeQty,
                    LastUpdated = x.LastUpdated,

                    UnitCode = x.IngredientId.HasValue && x.Ingredient.BaseUnit != null
                        ? x.Ingredient.BaseUnit.UnitCode
                        : x.PreparedItemId.HasValue && x.PreparedItem.BaseUnit != null
                            ? x.PreparedItem.BaseUnit.UnitCode
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
                        .FirstOrDefault(),

                    CostEvidenceStatus = x.IngredientId.HasValue
                        ? "NOT_APPLICABLE"
                        : x.PreparedItemId.HasValue
                            ? string.Empty
                            : "LEGACY_NO_PREPARED_ITEM"
                })
                .OrderBy(x => x.StoreName)
                .ThenBy(x => x.IngredientName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            await EnrichPreparedItemCostLayersAsync(data);

            return (data, total);
        }

        private async Task EnrichPreparedItemCostLayersAsync(List<InventoryDTO> rows)
        {
            var preparedRows = rows
                .Where(x => x.PreparedItemId.HasValue)
                .ToList();
            if (preparedRows.Count == 0)
                return;

            var preparedItemIds = preparedRows
                .Select(x => x.PreparedItemId!.Value)
                .Distinct()
                .ToList();
            var storeIds = preparedRows
                .Select(x => x.StoreId)
                .Distinct()
                .ToList();

            var layers = await _context.InventoryCostLayers
                .AsNoTracking()
                .Where(x => x.PreparedItemId.HasValue
                    && preparedItemIds.Contains(x.PreparedItemId.Value)
                    && storeIds.Contains(x.StoreId))
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.InventoryCostLayerId)
                .Select(x => new
                {
                    x.InventoryCostLayerId,
                    x.StoreId,
                    PreparedItemId = x.PreparedItemId!.Value,
                    x.UnitCost,
                    x.SourceProductionRunId,
                    x.CreatedAt
                })
                .ToListAsync();

            var latestByIdentity = layers
                .GroupBy(x => (x.StoreId, x.PreparedItemId))
                .ToDictionary(x => x.Key, x => x.First());

            foreach (var row in preparedRows)
            {
                if (!latestByIdentity.TryGetValue((row.StoreId, row.PreparedItemId!.Value), out var layer))
                {
                    row.CostEvidenceStatus = "MISSING_ACTUAL_LAYER";
                    row.LastUnitPrice = null;
                    continue;
                }

                row.LatestCostLayerId = layer.InventoryCostLayerId;
                row.LatestCostLayerAt = layer.CreatedAt;
                row.SourceProductionRunId = layer.SourceProductionRunId;
                row.LastUnitPrice = layer.UnitCost;
                row.CostEvidenceStatus = "ACTUAL_LAYER";
                row.LastSupplierName = null;
            }
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
                    PreparedItemId = x.StoreInventory.PreparedItemId,
                    IngredientName = x.StoreInventory.IngredientId.HasValue
                        ? x.StoreInventory.Ingredient.Name
                        : null,
                    PreparedItemName = x.StoreInventory.PreparedItemId.HasValue
                        ? x.StoreInventory.PreparedItem.Name
                        : null,
                    UnitCode = x.StoreInventory.IngredientId.HasValue &&
                               x.StoreInventory.Ingredient.BaseUnit != null
                        ? x.StoreInventory.Ingredient.BaseUnit.UnitCode
                        : x.StoreInventory.PreparedItemId.HasValue &&
                          !x.StoreInventory.RecipeId.HasValue &&
                          x.StoreInventory.PreparedItem.BaseUnit != null
                            ? x.StoreInventory.PreparedItem.BaseUnit.UnitCode
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
                        : !string.IsNullOrWhiteSpace(x.PreparedItemName)
                            ? x.PreparedItemName
                        : x.RecipeId.HasValue
                            ? $"Công thức #{x.RecipeId.Value}"
                            : "Không xác định",

                    IdentityBadge = x.PreparedItemId.HasValue && x.RecipeId.HasValue
                        ? "BTP liên kết"
                        : x.PreparedItemId.HasValue
                            ? "BTP"
                            : x.RecipeId.HasValue
                                ? "BTP legacy"
                                : string.Empty,
                    QuantitySemanticsStatus = x.IngredientId.HasValue
                        ? CafeChain.Application.DTOs.Inventories.QuantitySemanticsStatuses.NotApplicable
                        : x.RecipeId.HasValue
                            ? CafeChain.Application.DTOs.Inventories.QuantitySemanticsStatuses.Unknown
                            : CafeChain.Application.DTOs.Inventories.QuantitySemanticsStatuses.BaseUnitQuantityConfirmed,

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
                return $"POS / Đơn hàng #CC{referenceOrderId.Value:D5}";

            if (inventoryTransferId.HasValue)
                return $"Chuyển kho #{inventoryTransferId.Value}";

            if (inventoryDocumentId.HasValue)
                return $"Phiếu kho #{inventoryDocumentId.Value}";

            return "Giao dịch kho";
        }
    }
}
