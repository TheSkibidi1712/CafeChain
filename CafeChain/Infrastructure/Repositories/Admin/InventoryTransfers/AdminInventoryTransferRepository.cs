using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryTransfers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Inventories.Transfers;
using CafeChain.Models.Stores;
using CafeChain.ViewModels.Admin.InventoryDocuments.Dropdown;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace CafeChain.Infrastrusture.Repositories.Admin.InventoryTransfers
{
    public class AdminInventoryTransferRepository : IAdminInventoryTransferRepository
    {
        private readonly AppDbContext _context;
        private IDbContextTransaction? _transaction;

        public AdminInventoryTransferRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task BeginTransactionAsync()
        {
            _transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
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

        public async Task AddTransferAsync(InventoryTransfer transfer)
        {
            await _context.InventoryTransfers.AddAsync(transfer);
        }

        public async Task<InventoryTransfer?> GetTransferByIdAsync(int id)
        {
            return await _context.InventoryTransfers
                .Include(x => x.Details)
                    .ThenInclude(x => x.Ingredient)
                        .ThenInclude(x => x.BaseUnit)
                .Include(x => x.Details)
                    .ThenInclude(x => x.Unit)
                .Include(x => x.Details)
                    .ThenInclude(x => x.PreparedItem)
                        .ThenInclude(x => x.BaseUnit)
                .Include(x => x.FromStore)
                .Include(x => x.ToStore)
                .Include(x => x.CreatedByStaff)
                .Include(x => x.ConfirmedByStaff)
                .Include(x => x.CancelledByStaff)
                .FirstOrDefaultAsync(x => x.InventoryTransferId == id);
        }

        public async Task<List<InventoryTransfer>> GetTransfersAsync(
            string? keyword,
            InventoryTransferStatus? status,
            int? fromStoreId,
            int? toStoreId,
            int skip,
            int take,
            IReadOnlyCollection<int>? allowedStoreIds = null)
        {
            return await BuildTransferIndexQuery(keyword, status, fromStoreId, toStoreId, allowedStoreIds)
                .OrderByDescending(x => x.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<int> CountTransfersAsync(
            string? keyword,
            InventoryTransferStatus? status,
            int? fromStoreId,
            int? toStoreId,
            IReadOnlyCollection<int>? allowedStoreIds = null)
        {
            return await BuildTransferIndexQuery(keyword, status, fromStoreId, toStoreId, allowedStoreIds)
                .CountAsync();
        }

        private IQueryable<InventoryTransfer> BuildTransferIndexQuery(
            string? keyword,
            InventoryTransferStatus? status,
            int? fromStoreId,
            int? toStoreId,
            IReadOnlyCollection<int>? allowedStoreIds)
        {
            var query = _context.InventoryTransfers
                .AsNoTracking()
                .Include(x => x.FromStore)
                .Include(x => x.ToStore)
                .Include(x => x.CreatedByStaff)
                .Include(x => x.Details)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var normalizedKeyword = keyword.Trim();
                query = query.Where(x => x.Code.Contains(normalizedKeyword));
            }

            if (status.HasValue)
            {
                query = query.Where(x => x.Status == status.Value);
            }

            if (fromStoreId.HasValue && fromStoreId.Value > 0)
            {
                query = query.Where(x => x.FromStoreId == fromStoreId.Value);
            }

            if (toStoreId.HasValue && toStoreId.Value > 0)
            {
                query = query.Where(x => x.ToStoreId == toStoreId.Value);
            }

            if (allowedStoreIds != null)
            {
                var scopedStoreIds = allowedStoreIds.Distinct().ToList();
                query = query.Where(x =>
                    scopedStoreIds.Contains(x.FromStoreId)
                    || scopedStoreIds.Contains(x.ToStoreId));
            }

            return query;
        }

        public void UpdateTransfer(InventoryTransfer transfer)
        {
            _context.InventoryTransfers.Update(transfer);
        }

        public void RemoveTransferDetails(IEnumerable<InventoryTransferDetail> details)
        {
            _context.InventoryTransferDetails.RemoveRange(details);
        }

        public async Task<List<Store>> GetStoresByIdsAsync(List<int> ids)
        {
            return await _context.Stores
                .Where(x => ids.Contains(x.StoreId))
                .ToListAsync();
        }

        public async Task<List<StoreDropdownVM>> GetStoreDropdownAsync()
        {
            return await _context.Stores
                .AsNoTracking()
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .Select(x => new StoreDropdownVM
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
                .FirstOrDefaultAsync(x => x.IngredientId == ingredientId);
        }

        public async Task<PreparedItem?> GetPreparedItemAsync(int preparedItemId)
        {
            return await _context.PreparedItems
                .Include(x => x.BaseUnit)
                .FirstOrDefaultAsync(x => x.PreparedItemId == preparedItemId);
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

        public async Task<List<StoreInventory>> GetStoreInventoriesAsync(int storeId)
        {
            return await _context.StoreInventories
                .AsNoTracking()
                .Include(x => x.Ingredient)
                    .ThenInclude(x => x.BaseUnit)
                .Include(x => x.Ingredient)
                    .ThenInclude(x => x.UnitConversions)
                        .ThenInclude(x => x.FromUnit)
                .Include(x => x.PreparedItem)
                    .ThenInclude(x => x.BaseUnit)
                .Where(x => x.StoreId == storeId &&
                    (x.IngredientId.HasValue || x.PreparedItemId.HasValue))
                .ToListAsync();
        }

        public async Task<StoreInventory?> GetStoreInventoryForUpdateAsync(int storeId, int ingredientId)
        {
            return await _context.StoreInventories
                .FromSqlInterpolated(
                    $"SELECT * FROM StoreInventories WITH (UPDLOCK, ROWLOCK) WHERE StoreId = {storeId} AND IngredientId = {ingredientId}")
                .FirstOrDefaultAsync();
        }

        public async Task<StoreInventory> GetOrCreateStoreInventoryForUpdateAsync(int storeId, int ingredientId)
        {
            var inventory = await GetStoreInventoryForUpdateAsync(storeId, ingredientId);

            if (inventory != null)
            {
                return inventory;
            }

            inventory = new StoreInventory
            {
                StoreId = storeId,
                IngredientId = ingredientId,
                AvailableQty = 0,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow
            };

            await _context.StoreInventories.AddAsync(inventory);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _context.Entry(inventory).State = EntityState.Detached;

                var existing = await GetStoreInventoryForUpdateAsync(storeId, ingredientId);

                if (existing != null)
                {
                    return existing;
                }

                throw;
            }

            return inventory;
        }

        public async Task<StoreInventory> GetOrCreatePreparedItemInventoryForUpdateAsync(
            int storeId,
            int preparedItemId,
            int actorAccountId,
            string evidenceReference)
        {
            var inventory = await _context.StoreInventories
                .FromSqlInterpolated(
                    $@"SELECT * FROM StoreInventories WITH (UPDLOCK, ROWLOCK)
                       WHERE StoreId = {storeId} AND PreparedItemId = {preparedItemId}
                         AND BtpIdentityState = {(int)BtpIdentityState.Canonical}")
                .FirstOrDefaultAsync();
            if (inventory != null)
                return inventory;

            inventory = new StoreInventory
            {
                StoreId = storeId,
                IngredientId = null,
                RecipeId = null,
                PreparedItemId = preparedItemId,
                BtpIdentityState = BtpIdentityState.Canonical,
                QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
                QuantitySemanticsEvidenceReference = evidenceReference,
                QuantitySemanticsReviewedAt = DateTime.UtcNow,
                QuantitySemanticsReviewedByAccountId = actorAccountId,
                AvailableQty = 0,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow
            };

            await _context.StoreInventories.AddAsync(inventory);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _context.Entry(inventory).State = EntityState.Detached;
                var existing = await _context.StoreInventories
                    .FromSqlInterpolated(
                        $@"SELECT * FROM StoreInventories WITH (UPDLOCK, ROWLOCK)
                           WHERE StoreId = {storeId} AND PreparedItemId = {preparedItemId}
                             AND BtpIdentityState = {(int)BtpIdentityState.Canonical}")
                    .FirstOrDefaultAsync();
                if (existing != null)
                    return existing;
                throw;
            }

            return inventory;
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

        public async Task AddInventoryTransactionAsync(InventoryTransaction transaction)
        {
            await _context.InventoryTransactions.AddAsync(transaction);
        }

        public async Task AddCostLayerAsync(InventoryCostLayer layer)
        {
            await _context.InventoryCostLayers.AddAsync(layer);
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

        public async Task<List<InventoryCostLayer>> GetAvailablePreparedItemCostLayersAsync(
            int storeId,
            int preparedItemId)
        {
            return await _context.InventoryCostLayers
                .Where(x =>
                    x.StoreId == storeId &&
                    x.PreparedItemId == preparedItemId &&
                    x.RemainingQuantity > 0)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();
        }

        public Task<int?> GetAccountIdForStaffAsync(int staffId) =>
            _context.Staffs
                .AsNoTracking()
                .Where(s => s.StaffId == staffId)
                .Select(s => (int?)s.AccountId)
                .FirstOrDefaultAsync();

        public void UpdateCostLayer(InventoryCostLayer layer)
        {
            _context.InventoryCostLayers.Update(layer);
        }

        public async Task<string> GenerateTransferCodeAsync()
        {
            var today = DateTime.Today;
            var count = await _context.InventoryTransfers
                .CountAsync(x => x.DocumentDate.Date == today);

            return $"CK-{today:yyyyMMdd}-{count + 1:000}";
        }

        public async Task<List<InventoryTransfer>> GetPendingTransfersToStoreAsync(int storeId)
        {
            return await _context.InventoryTransfers
                .Include(x => x.FromStore)
                .Include(x => x.ToStore)
                .Include(x => x.Details)
                .Where(x =>
                    x.ToStoreId == storeId
                    && x.Status == InventoryTransferStatus.DRAFT)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }
    }
}
