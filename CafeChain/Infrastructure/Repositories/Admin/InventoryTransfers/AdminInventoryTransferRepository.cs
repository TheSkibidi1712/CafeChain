using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryTransfers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Ingredients;
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
                .Include(x => x.FromStore)
                .Include(x => x.ToStore)
                .Include(x => x.CreatedByStaff)
                .Include(x => x.ConfirmedByStaff)
                .Include(x => x.CancelledByStaff)
                .FirstOrDefaultAsync(x => x.InventoryTransferId == id);
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
                .Where(x => x.StoreId == storeId && x.IngredientId.HasValue)
                .ToListAsync();
        }

        public async Task<StoreInventory?> GetStoreInventoryForUpdateAsync(int storeId, int ingredientId)
        {
            return await _context.StoreInventories
                .FromSqlInterpolated(
                    $"SELECT * FROM StoreInventories WITH (UPDLOCK, ROWLOCK) WHERE StoreId = {storeId} AND IngredientId = {ingredientId}")
                .FirstOrDefaultAsync();
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
