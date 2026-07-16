using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Inventories.Auditing;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Documents;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Inventories.Transfers;
using CafeChain.Models.Inventories.Approvals;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using CafeChain.ViewModels.Admin.InventoryDocuments.Dropdown;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Infrastrusture.Repositories.Admin.InventoryDocuments
{
    public class AdminInventoryDocumentRepository : IAdminInventoryDocumentRepository
    {
        private readonly AppDbContext _context;

        private IDbContextTransaction? _transaction;

        public AdminInventoryDocumentRepository(AppDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // TRANSACTION
        // =====================================================

        public async Task BeginTransactionAsync()
        {
            _transaction =
                await _context.Database
                    .BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            if (_transaction == null)
            {
                return;
            }

            await _transaction.CommitAsync();

            await _transaction.DisposeAsync();

            _transaction = null;
        }

        public async Task RollbackTransactionAsync()
        {
            if (_transaction == null)
            {
                return;
            }

            await _transaction.RollbackAsync();

            await _transaction.DisposeAsync();

            _transaction = null;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        // =====================================================
        // QUERY
        // =====================================================

        public IQueryable<InventoryDocument>
            GetDocumentsQuery()
        {
            return _context.InventoryDocuments

                .Include(x => x.Store)

                .Include(x => x.Staff)

                .Include(x => x.Supplier)

                .Include(x => x.NegativeApproval)
                    .ThenInclude(x => x.RequesterStaff)

                .Include(x => x.NegativeApproval)
                    .ThenInclude(x => x.ApproverStaff)

                .Include(x => x.NegativeApproval)
                    .ThenInclude(x => x.Lines)

                .AsNoTracking();
        }

        public async Task<InventoryDocument?> GetByIdAsync(int documentId)
        {
            return await _context.InventoryDocuments
                .FirstOrDefaultAsync(x =>
                    x.InventoryDocumentId == documentId);
        }

        public async Task<List<InventoryNegativeCostGap>> GetNegativeCostGapsByDocumentAsync(int documentId)
        {
            return await _context.InventoryNegativeCostGaps
                .AsNoTracking()
                .Include(x => x.Settlements)
                .Where(x => x.InventoryDocumentDetailId.HasValue
                    && _context.InventoryDocumentDetails.Any(d =>
                        d.InventoryDocumentDetailId == x.InventoryDocumentDetailId.Value
                        && d.InventoryDocumentId == documentId))
                .OrderBy(x => x.OccurredAt)
                .ThenBy(x => x.InventoryNegativeCostGapId)
                .ToListAsync();
        }

        public async Task<InventoryDocument?> GetDocumentForConfirmAsync(int documentId)
        {
            IQueryable<InventoryDocument> query = _context.InventoryDocuments;
            if (_context.Database.IsSqlServer() && _context.Database.CurrentTransaction != null)
                query = _context.InventoryDocuments.FromSqlInterpolated($"SELECT * FROM InventoryDocuments WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE InventoryDocumentId = {documentId}");

            return await query

                .Include(x => x.Store)

                .Include(x => x.Staff)

                .Include(x => x.Supplier)

                .Include(x => x.NegativeApproval)
                    .ThenInclude(x => x.Lines)

                .Include(x => x.Details)
                    .ThenInclude(x => x.Ingredient)
                        .ThenInclude(x => x.BaseUnit)

                .Include(x => x.Details)
                    .ThenInclude(x => x.Unit)

                .FirstOrDefaultAsync(x =>
                    x.InventoryDocumentId == documentId);
        }

        public async Task<InventoryDocument?> GetDocumentWithDetailsAsync(int documentId)
        {
            return await _context.InventoryDocuments

                .AsNoTracking()

                .Include(x => x.Store)

                .Include(x => x.Staff)

                .Include(x => x.Supplier)

                .Include(x => x.NegativeApproval)
                    .ThenInclude(x => x.RequesterStaff)

                .Include(x => x.NegativeApproval)
                    .ThenInclude(x => x.ApproverStaff)

                .Include(x => x.NegativeApproval)
                    .ThenInclude(x => x.Lines)

                .Include(x => x.Details)
                    .ThenInclude(x => x.Ingredient)
                        .ThenInclude(x => x.BaseUnit)

                .Include(x => x.Details)
                    .ThenInclude(x => x.Unit)

                .FirstOrDefaultAsync(x =>
                    x.InventoryDocumentId == documentId);
        }

        public void UpdateDocument(InventoryDocument document)
        {
            _context.InventoryDocuments.Update(document);
        }

        // =====================================================
        // DOCUMENT DETAIL
        // =====================================================
        public void UpdateDocumentDetail(InventoryDocumentDetail detail)
        {
            _context.InventoryDocumentDetails
                .Update(detail);
        }

        // =====================================================
        // SNAPSHOT
        // =====================================================

        public async Task<bool> SnapshotExistsAsync(int documentId)
        {
            return await _context.InventoryDocumentSnapshots.AnyAsync(x => x.InventoryDocumentId == documentId);
        }

        public async Task<InventoryDocumentSnapshot?> GetSnapshotAsync(int documentId)
        {
            return await _context.InventoryDocumentSnapshots

                .Include(x => x.Details)

                .AsNoTracking()

                .FirstOrDefaultAsync(x =>
                    x.InventoryDocumentId == documentId);
        }

        public async Task AddSnapshotAsync( InventoryDocumentSnapshot snapshot)
        {
            await _context.InventoryDocumentSnapshots.AddAsync(snapshot);
        }

        public async Task AddSnapshotDetailsAsync(IEnumerable<InventoryDocumentSnapshotDetail> details)
        {
            await _context.InventoryDocumentSnapshotDetails.AddRangeAsync(details);
        }

        // =====================================================
        // CREATE DOCUMENT
        // =====================================================

        public async Task AddDocumentAsync(InventoryDocument document)
        {
            await _context.InventoryDocuments
                .AddAsync(document);
        }

        public async Task AddDocumentDetailsAsync(IEnumerable<InventoryDocumentDetail> details)
        {
            await _context.InventoryDocumentDetails
                .AddRangeAsync(details);
        }

        // =====================================================
        // STORE INVENTORY
        // =====================================================

        public async Task<StoreInventory?> GetStoreInventoryAsync(int storeId, int ingredientId)
        {
            return await _context.StoreInventories

                .FirstOrDefaultAsync(x =>
                    x.StoreId == storeId
                    && x.IngredientId == ingredientId);
        }

        public async Task<StoreInventory> GetOrCreateStoreInventoryForIngredientAsync(int storeId, int ingredientId)
        {
            var inventory =
                await GetStoreInventoryAsync(storeId, ingredientId);

            if (inventory != null)
            {
                return inventory;
            }

            inventory =
                new StoreInventory
                {
                    StoreId = storeId,
                    IngredientId = ingredientId,
                    AvailableQty = 0,
                    ReservedQty = 0,
                    LastUpdated = DateTime.UtcNow
                };

            await _context.StoreInventories.AddAsync(inventory);
            await _context.SaveChangesAsync();

            return inventory;
        }

        public async Task<List<StoreInventory>> GetStoreInventoriesAsync(int storeId, CancellationToken cancellationToken = default)
        {
            return await _context.StoreInventories

                .Include(x => x.Ingredient)
                    .ThenInclude(x => x.BaseUnit)

                .Include(x => x.Ingredient)
                    .ThenInclude(x => x.UnitConversions)
                        .ThenInclude(x => x.FromUnit)

                .Where(x => x.StoreId == storeId)

                .ToListAsync(cancellationToken);
        }

        public async Task AddStoreInventoryAsync(StoreInventory inventory)
        {
            await _context.StoreInventories.AddAsync(inventory);
        }

        public void UpdateStoreInventory(StoreInventory inventory)
        {
            inventory.LastUpdated = DateTime.UtcNow;

            _context.StoreInventories.Update(inventory);
        }

        // =====================================================
        // INVENTORY TRANSACTION
        // =====================================================

        public async Task AddInventoryTransactionAsync(InventoryTransaction transaction)
        {
            await _context.InventoryTransactions.AddAsync(transaction);
        }

        public async Task AddInventoryTransactionsAsync(IEnumerable<InventoryTransaction> transactions)
        {
            await _context.InventoryTransactions.AddRangeAsync(transactions);
        }

        // =====================================================
        // COST LAYER
        // =====================================================

        public async Task AddCostLayerAsync(InventoryCostLayer layer)
        {
            await _context.InventoryCostLayers.AddAsync(layer);
        }

        public async Task AddCostLayersAsync(IEnumerable<InventoryCostLayer> layers)
        {
            await _context.InventoryCostLayers.AddRangeAsync(layers);
        }

        public async Task<List<InventoryCostLayer>> GetAvailableCostLayersAsync(int storeId, int ingredientId)
        {
            return await _context.InventoryCostLayers

                .Where(x =>
                    x.StoreId == storeId
                    && x.IngredientId == ingredientId
                    && x.RemainingQuantity > 0)

                .OrderBy(x => x.CreatedAt)

                .ToListAsync();
        }

        public async Task<List<InventoryCostLayer>> GetAvailableCostLayersAsync(int storeId, IEnumerable<int> ingredientIds)
        {
            var ids =
                ingredientIds
                    .Where(x => x > 0)
                    .Distinct()
                    .ToList();

            if (ids.Count == 0)
            {
                return [];
            }

            return await _context.InventoryCostLayers
                .AsNoTracking()
                .Where(x =>
                    x.StoreId == storeId
                    && x.IngredientId != null
                    && ids.Contains(x.IngredientId.Value)
                    && x.RemainingQuantity > 0)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<decimal> GetAvailableQuantityAsync(int storeId, int ingredientId)
        {
            return await _context.StoreInventories

                .Where(x =>
                    x.StoreId == storeId &&
                    x.IngredientId == ingredientId)

                .Select(x => x.AvailableQty)

                .FirstOrDefaultAsync();
        }

        public async Task<InventoryCostLayer?> GetCostLayerByIdAsync(int costLayerId)
        {
            return await _context.InventoryCostLayers

                .FirstOrDefaultAsync(x =>
                    x.InventoryCostLayerId == costLayerId);
        }

        public async Task<InventoryCostLayer?> GetLatestCostLayerAsync(int storeId, int ingredientId)
        {
            return await _context.InventoryCostLayers
                .AsNoTracking()
                .Where(x =>
                    x.StoreId == storeId
                    && x.IngredientId == ingredientId
                    && x.Quantity > 0)
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.InventoryCostLayerId)
                .FirstOrDefaultAsync();
        }


        public void UpdateCostLayer(InventoryCostLayer layer)
        {
            _context.InventoryCostLayers.Update(layer);
        }

        // =====================================================
        // COST ALLOCATION
        // =====================================================

        public async Task AddCostAllocationAsync(InventoryCostAllocation allocation)
        {
            await _context.InventoryCostAllocations.AddAsync(allocation);
        }

        public async Task AddCostAllocationsAsync(IEnumerable<InventoryCostAllocation> allocations)
        {
            await _context.InventoryCostAllocations.AddRangeAsync(allocations);
        }

        public void RemoveDocumentDetails(IEnumerable<InventoryDocumentDetail> details)
        {
            _context.InventoryDocumentDetails.RemoveRange(details);
        }

        public async Task<StoreInventory?> GetStoreInventoryForUpdateAsync(int storeId, int ingredientId)
        {
            if (_context.Database.IsSqlServer())
            {
                return await _context.StoreInventories.FromSqlInterpolated(
                    $"SELECT * FROM StoreInventories WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE StoreId = {storeId} AND IngredientId = {ingredientId} AND PreparedItemId IS NULL")
                    .SingleOrDefaultAsync();
            }

            return await GetStoreInventoryAsync(storeId, ingredientId);
        }

        public async Task AddNegativeApprovalAsync(InventoryNegativeApproval approval)
        {
            await _context.InventoryNegativeApprovals.AddAsync(approval);
        }

        public async Task<InventoryNegativeApproval?> GetNegativeApprovalForUpdateAsync(int documentId)
        {
            IQueryable<InventoryNegativeApproval> query = _context.InventoryNegativeApprovals;
            if (_context.Database.IsSqlServer())
                query = _context.InventoryNegativeApprovals.FromSqlInterpolated($"SELECT * FROM InventoryNegativeApprovals WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE InventoryDocumentId = {documentId}");

            return await query.Include(x => x.Lines).SingleOrDefaultAsync(x => x.InventoryDocumentId == documentId);
        }

        public void UpdateNegativeApproval(InventoryNegativeApproval approval)
        {
            _context.InventoryNegativeApprovals.Update(approval);
        }

        public async Task AddInventoryNegativeCostGapAsync(InventoryNegativeCostGap gap)
        {
            await _context.InventoryNegativeCostGaps.AddAsync(gap);
        }

        public async Task<List<InventoryNegativeCostGap>> GetOpenCostGapsForUpdateAsync(int storeInventoryId)
        {
            IQueryable<InventoryNegativeCostGap> query = _context.InventoryNegativeCostGaps
                .Where(x => x.StoreInventoryId == storeInventoryId && x.OutstandingQuantity > 0);
            if (_context.Database.IsSqlServer())
            {
                query = _context.InventoryNegativeCostGaps.FromSqlInterpolated(
                    $"SELECT * FROM InventoryNegativeCostGaps WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE StoreInventoryId = {storeInventoryId} AND OutstandingQuantity > 0");
            }

            return await query.OrderBy(x => x.OccurredAt).ThenBy(x => x.InventoryNegativeCostGapId).ToListAsync();
        }

        public async Task AddCostGapSettlementsAsync(IEnumerable<InventoryCostGapSettlement> settlements)
        {
            await _context.InventoryCostGapSettlements.AddRangeAsync(settlements);
        }

        // =====================================================
        // INVENTORY DEBT
        // =====================================================

        // =====================================================
        // MASTER DATA
        // =====================================================
        public async Task<List<StoreDropdownVM>> GetStoreDropdownAsync()
        {
            return await _context.Stores

                .AsNoTracking()

                .OrderBy(x => x.Name)

                .Select(x =>
                    new StoreDropdownVM
                    {
                        StoreId = x.StoreId,
                        StoreName = x.Name
                    })

                .ToListAsync();
        }

        public async Task<Ingredient?> GetIngredientAsync(int ingredientId)
        {
            return await _context.Ingredients
                .Include(x => x.BaseUnit)
                .Include(x => x.UnitConversions)
                    .ThenInclude(x => x.FromUnit)
                .FirstOrDefaultAsync(x =>
                    x.IngredientId == ingredientId);
        }

        public async Task<Unit?> GetUnitAsync(int unitId)
        {
            return await _context.Units
                .FirstOrDefaultAsync(x =>
                    x.UnitId == unitId);
        }

        public async Task<Store?> GetStoreAsync(int storeId)
        {
            return await _context.Stores
                .FirstOrDefaultAsync(x =>
                    x.StoreId == storeId);
        }

        public async Task<Supplier?> GetSupplierAsync(int supplierId)
        {
            return await _context.Suppliers
                .FirstOrDefaultAsync(x =>
                    x.SupplierId == supplierId);
        }

        public async Task<Staff?> GetStaffAsync(int staffId)
        {
            return await _context.Staffs
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.StaffId == staffId);
        }

        public async Task<string> GenerateDocumentCodeAsync(
            InventoryDocumentType type,
            InventoryDocumentPurpose? purpose = null)
        {
            string prefix = (type, purpose) switch
            {
                (InventoryDocumentType.IMPORT, InventoryDocumentPurpose.IMPORT_INTERNAL) => "NNB",
                (InventoryDocumentType.IMPORT, InventoryDocumentPurpose.IMPORT_ADJUSTMENT) => "DCN",
                (InventoryDocumentType.IMPORT, _) => "PN",
                (InventoryDocumentType.EXPORT, _) => "PX",
                (InventoryDocumentType.STOCK_TAKE, _) => "KK",
                (InventoryDocumentType.WASTE, _) => "HU",
                (InventoryDocumentType.PRODUCTION_IN, _) => "SPN",
                (InventoryDocumentType.PRODUCTION_OUT, _) => "SPX",
                (InventoryDocumentType.SALES_DEDUCTION, _) => "HBH",
                (InventoryDocumentType.ADJUSTMENT_IN, _) => "DCN",
                (InventoryDocumentType.INTERNAL_IMPORT, _) => "NNB",
                _ => "PK"
            };

            var today = DateTime.Today;
            var next = await CafeChain.Infrastrusture.Repositories.DocumentNumberCounterAllocator.NextAsync(
                _context,
                $"InventoryDocument:{prefix}",
                today);
            return $"{prefix}-{today:yyyyMMdd}-{next:000}";
        }

        public async Task<List<SupplierDropdownVM>>GetSupplierDropdownAsync()
        {
            return await _context.Suppliers
                .AsNoTracking()
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .Select(x =>
                    new SupplierDropdownVM
                    {
                        SupplierId = x.SupplierId,
                        SupplierName = x.Name!
                    })
                .ToListAsync();
        }

        public async Task<List<IngredientSupplier>> GetSupplierIngredientsAsync(int supplierId, CancellationToken cancellationToken = default)
        {
            return await _context.IngredientSuppliers
                .Include(x => x.Ingredient)
                    .ThenInclude(x => x.BaseUnit)
                .Include(x => x.Ingredient)
                    .ThenInclude(x => x.UnitConversions)
                        .ThenInclude(x => x.FromUnit)
                .Include(x => x.Unit)
                .Include(x => x.PriceHistories)
                .Where(x =>
                    x.SupplierId == supplierId &&
                    x.Active)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<Ingredient>> GetActiveIngredientsAsync()
        {
            return await _context.Ingredients
                .AsNoTracking()
                .Include(x => x.BaseUnit)
                .Include(x => x.UnitConversions)
                    .ThenInclude(x => x.FromUnit)
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .ToListAsync();
        }

        public async Task<List<IngredientSupplier>> GetActiveIngredientSuppliersByIngredientIdsAsync(IEnumerable<int> ingredientIds)
        {
            var ids =
                ingredientIds
                    .Where(x => x > 0)
                    .Distinct()
                    .ToList();

            if (ids.Count == 0)
            {
                return [];
            }

            return await _context.IngredientSuppliers
                .AsNoTracking()
                .Include(x => x.Unit)
                .Include(x => x.PriceHistories)
                .Include(x => x.Ingredient)
                    .ThenInclude(x => x.BaseUnit)
                .Include(x => x.Ingredient)
                    .ThenInclude(x => x.UnitConversions)
                        .ThenInclude(x => x.FromUnit)
                .Where(x =>
                    ids.Contains(x.IngredientId)
                    && x.Active)
                .ToListAsync();
        }

        public async Task<List<IngredientSupplier>> GetActiveIngredientSuppliersByIdsAsync(
            IEnumerable<int> ingredientSupplierIds)
        {
            var ids = ingredientSupplierIds.Where(x => x > 0).Distinct().ToList();
            if (ids.Count == 0) return [];

            return await _context.IngredientSuppliers
                .AsNoTracking()
                .Include(x => x.Supplier)
                .Include(x => x.Unit)
                .Include(x => x.Ingredient).ThenInclude(x => x.BaseUnit)
                .Include(x => x.Ingredient).ThenInclude(x => x.UnitConversions).ThenInclude(x => x.FromUnit)
                .Where(x => ids.Contains(x.IngredientSupplierId) && x.Active)
                .ToListAsync();
        }

        public Task<bool> IsActiveSupplierStoreAsync(int supplierId, int storeId) =>
            _context.SupplierStores.AsNoTracking().AnyAsync(x =>
                x.SupplierId == supplierId
                && x.StoreId == storeId
                && x.Active
                && x.Supplier.Active
                && x.Store.Active);

        // =====================================================
        // AUDIT LOG
        // =====================================================

        public async Task AddAuditLogAsync(
            AuditLog auditLog)
        {
            await _context.AuditLogs
                .AddAsync(auditLog);
        }
    }
}
