using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryTransfers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Stock;
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
                .Include(x => x.ParentInventoryTransfer)
                .FirstOrDefaultAsync(x => x.InventoryTransferId == id);
        }

        public async Task<InventoryTransfer?> GetTransferForUpdateAsync(int id)
        {
            IQueryable<InventoryTransfer> query = _context.InventoryTransfers;
            if (_context.Database.IsSqlServer())
                query = _context.InventoryTransfers.FromSqlInterpolated($"SELECT * FROM InventoryTransfers WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE InventoryTransferId = {id}");

            return await query
                .Include(x => x.Details).ThenInclude(x => x.Ingredient).ThenInclude(x => x.BaseUnit)
                .Include(x => x.Details).ThenInclude(x => x.PreparedItem).ThenInclude(x => x.BaseUnit)
                .Include(x => x.Details).ThenInclude(x => x.Unit)
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
            if (_context.Database.IsSqlServer())
            {
                return await _context.StoreInventories
                    .FromSqlInterpolated(
                        $"SELECT * FROM StoreInventories WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE StoreId = {storeId} AND IngredientId = {ingredientId} AND PreparedItemId IS NULL")
                    .FirstOrDefaultAsync();
            }

            return await _context.StoreInventories.FirstOrDefaultAsync(
                x => x.StoreId == storeId
                     && x.IngredientId == ingredientId
                     && x.PreparedItemId == null);
        }

        public async Task LockInventoriesAsync(
            IEnumerable<(int StoreId, int? IngredientId, int? PreparedItemId)> identities)
        {
            var ordered = identities
                .Distinct()
                .OrderBy(x => x.StoreId)
                .ThenBy(x => x.IngredientId.HasValue ? 0 : 1)
                .ThenBy(x => x.IngredientId ?? x.PreparedItemId)
                .ToList();

            foreach (var identity in ordered)
            {
                if (identity.IngredientId.HasValue)
                {
                    _ = await GetStoreInventoryForUpdateAsync(
                        identity.StoreId,
                        identity.IngredientId.Value);
                    continue;
                }

                if (!identity.PreparedItemId.HasValue)
                    throw new InvalidOperationException("INVALID_INVENTORY_IDENTITY");

                if (_context.Database.IsSqlServer())
                {
                    _ = await _context.StoreInventories
                        .FromSqlInterpolated(
                            $@"SELECT * FROM StoreInventories WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                               WHERE StoreId = {identity.StoreId}
                                 AND PreparedItemId = {identity.PreparedItemId.Value}
                                 AND BtpIdentityState = {(int)BtpIdentityState.Canonical}")
                        .FirstOrDefaultAsync();
                }
                else
                {
                    _ = await _context.StoreInventories.FirstOrDefaultAsync(
                        x => x.StoreId == identity.StoreId
                             && x.PreparedItemId == identity.PreparedItemId.Value
                             && x.BtpIdentityState == BtpIdentityState.Canonical);
                }
            }
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
            var inventory = await GetPreparedItemInventoryForUpdateAsync(storeId, preparedItemId);
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
                var existing = await GetPreparedItemInventoryForUpdateAsync(storeId, preparedItemId);
                if (existing != null)
                    return existing;
                throw;
            }

            return inventory;
        }

        private async Task<StoreInventory?> GetPreparedItemInventoryForUpdateAsync(
            int storeId,
            int preparedItemId)
        {
            if (_context.Database.IsSqlServer())
            {
                return await _context.StoreInventories
                    .FromSqlInterpolated(
                        $@"SELECT * FROM StoreInventories WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                           WHERE StoreId = {storeId} AND PreparedItemId = {preparedItemId}
                             AND BtpIdentityState = {(int)BtpIdentityState.Canonical}")
                    .FirstOrDefaultAsync();
            }

            return await _context.StoreInventories.FirstOrDefaultAsync(
                x => x.StoreId == storeId
                     && x.PreparedItemId == preparedItemId
                     && x.BtpIdentityState == BtpIdentityState.Canonical);
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

        public async Task AddTransferCostAllocationsAsync(IEnumerable<InventoryTransferCostAllocation> allocations)
        {
            await _context.InventoryTransferCostAllocations.AddRangeAsync(allocations);
        }

        public async Task<List<InventoryTransferCostAllocation>> GetTransferCostAllocationsAsync(IEnumerable<int> detailIds)
        {
            var ids = detailIds.Distinct().ToList();
            return await _context.InventoryTransferCostAllocations
                .Where(x => ids.Contains(x.InventoryTransferDetailId))
                .OrderBy(x => x.InventoryTransferDetailId)
                .ThenBy(x => x.InventoryTransferCostAllocationId)
                .ToListAsync();
        }

        public async Task<List<InventoryTransferDiscrepancyPosting>> GetTransferDiscrepancyPostingsAsync(
            IEnumerable<int> detailIds)
        {
            var ids = detailIds.Distinct().ToList();
            return await _context.InventoryTransferDiscrepancyPostings
                .Where(x => ids.Contains(x.InventoryTransferDetailId))
                .Include(x => x.ActorStaff)
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.InventoryTransferDiscrepancyPostingId)
                .ToListAsync();
        }

        public async Task AddTransferDiscrepancyPostingsAsync(
            IEnumerable<InventoryTransferDiscrepancyPosting> postings)
        {
            await _context.InventoryTransferDiscrepancyPostings.AddRangeAsync(postings);
        }

        public async Task<List<BranchReceiptLine>> GetTransferReceiptLinesAsync(int transferId)
        {
            return await _context.BranchReceiptLines
                .AsNoTracking()
                .Include(x => x.BranchReceipt)
                    .ThenInclude(x => x.ConfirmedByStaff)
                .Where(x => x.BranchReceipt.SourceInventoryTransferId == transferId)
                .OrderBy(x => x.BranchReceipt.ConfirmedAt)
                .ThenBy(x => x.BranchReceiptLineId)
                .ToListAsync();
        }

        public async Task AddBranchReceiptAsync(BranchReceipt receipt)
        {
            await _context.BranchReceipts.AddAsync(receipt);
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
            var next = await CafeChain.Infrastrusture.Repositories.DocumentNumberCounterAllocator.NextAsync(
                _context,
                "InventoryTransfer:CK",
                today);
            return $"CK-{today:yyyyMMdd}-{next:000}";
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

        public async Task<List<InventoryTransfer>> GetLegacyDispatchedTransfersAsync()
        {
            return await _context.InventoryTransfers
                .AsNoTracking()
                .Include(x => x.Details).ThenInclude(x => x.Ingredient)
                .Include(x => x.Details).ThenInclude(x => x.PreparedItem)
                .Where(x => x.Status == InventoryTransferStatus.DISPATCHED
                    && x.Details.Any(d => d.DispatchedBaseQuantity > d.ReceivedBaseQuantity))
                .OrderBy(x => x.InventoryTransferId)
                .ToListAsync();
        }
    }
}
