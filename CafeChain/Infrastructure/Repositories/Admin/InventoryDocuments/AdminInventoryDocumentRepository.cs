using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Inventories.Auditing;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Debts;
using CafeChain.Models.Inventories.Documents;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Inventories.Transfers;
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

                .AsNoTracking();
        }

        public async Task<InventoryDocument?> GetByIdAsync(int documentId)
        {
            return await _context.InventoryDocuments
                .FirstOrDefaultAsync(x =>
                    x.InventoryDocumentId == documentId);
        }

        public async Task<InventoryDocument?> GetDocumentForConfirmAsync(int documentId)
        {
            return await _context.InventoryDocuments

                .Include(x => x.Store)

                .Include(x => x.Staff)

                .Include(x => x.Supplier)

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

        public async Task<List<StoreInventory>> GetStoreInventoriesAsync(int storeId)
        {
            return await _context.StoreInventories

                .Include(x => x.Ingredient)
                    .ThenInclude(x => x.BaseUnit)

                .Include(x => x.Ingredient)
                    .ThenInclude(x => x.UnitConversions)
                        .ThenInclude(x => x.FromUnit)

                .Where(x => x.StoreId == storeId)

                .ToListAsync();
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
        // STORE INVENTORY SNAPSHOT
        // =====================================================

        public async Task<StoreInventorySnapshot?> GetStoreInventorySnapshotAsync(int storeId, int ingredientId)
        {
            return await _context.StoreInventorySnapshots

                .FirstOrDefaultAsync(x =>
                    x.StoreId == storeId
                    && x.IngredientId == ingredientId);
        }

        public async Task AddStoreInventorySnapshotAsync(StoreInventorySnapshot snapshot)
        {
            await _context.StoreInventorySnapshots.AddAsync(snapshot);
        }

        public async Task AddStoreInventorySnapshotsAsync(IEnumerable<StoreInventorySnapshot> snapshots)
        {
            await _context.StoreInventorySnapshots.AddRangeAsync(snapshots);
        }

        public void UpdateStoreInventorySnapshot(StoreInventorySnapshot snapshot)
        {
            _context.StoreInventorySnapshots.Update(snapshot);
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
                    && ids.Contains(x.IngredientId)
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

        // =====================================================
        // INVENTORY DEBT
        // =====================================================

        public async Task AddDebtAsync(InventoryDebt debt)
        {
            await _context.InventoryDebts.AddAsync(debt);
        }

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

            string date = DateTime.Today.ToString("yyyyMMdd");

            var query = _context.InventoryDocuments
                .Where(x =>
                    x.Type == type
                    && x.DocumentDate.Date == DateTime.Today);

            if (purpose.HasValue)
            {
                query = query.Where(x => x.Purpose == purpose.Value);
            }

            int count = await query.CountAsync();

            return $"{prefix}-{date}-{count + 1:000}";
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

        public async Task<List<IngredientSupplier>> GetSupplierIngredientsAsync(int supplierId)
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
                .ToListAsync();
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

        public async Task<List<InventoryTransfer>> GetPendingTransfersToStoreAsync(int storeId)
        {
            return await _context.InventoryTransfers
                .AsNoTracking()
                .Include(x => x.FromStore)
                .Include(x => x.ToStore)
                .Where(x =>
                    x.ToStoreId == storeId
                    && x.Status == InventoryTransferStatus.DRAFT)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<InventoryTransfer?> GetTransferForInternalImportAsync(int transferId)
        {
            return await _context.InventoryTransfers
                .Include(x => x.FromStore)
                .Include(x => x.ToStore)
                .Include(x => x.Details)
                    .ThenInclude(x => x.Ingredient)
                        .ThenInclude(x => x.BaseUnit)
                .Include(x => x.Details)
                    .ThenInclude(x => x.Ingredient)
                        .ThenInclude(x => x.UnitConversions)
                .Include(x => x.Details)
                    .ThenInclude(x => x.Unit)
                .FirstOrDefaultAsync(x => x.InventoryTransferId == transferId);
        }

        public async Task AddTransferAsync(InventoryTransfer transfer)
        {
            await _context.InventoryTransfers.AddAsync(transfer);
        }

        public void UpdateTransfer(InventoryTransfer transfer)
        {
            _context.InventoryTransfers.Update(transfer);
        }

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
