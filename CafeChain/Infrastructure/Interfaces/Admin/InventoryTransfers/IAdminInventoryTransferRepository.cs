using CafeChain.Models.Inventories.Transfers;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Stores;
using CafeChain.Models.Inventories.Stock;
using CafeChain.ViewModels.Admin.InventoryDocuments.Dropdown;

namespace CafeChain.Infrastrusture.Interfaces.Admin.InventoryTransfers
{
    public interface IAdminInventoryTransferRepository
    {
        Task BeginTransactionAsync();

        Task CommitTransactionAsync();

        Task RollbackTransactionAsync();

        Task SaveChangesAsync();

        Task AddTransferAsync(InventoryTransfer transfer);

        Task<InventoryTransfer?> GetTransferByIdAsync(int id);
        Task<InventoryTransfer?> GetTransferForUpdateAsync(int id);

        Task<List<InventoryTransfer>> GetTransfersAsync(
            string? keyword,
            CafeChain.Models.Enums.Inventory.InventoryTransferStatus? status,
            int? fromStoreId,
            int? toStoreId,
            int skip,
            int take,
            IReadOnlyCollection<int>? allowedStoreIds = null);

        Task<int> CountTransfersAsync(
            string? keyword,
            CafeChain.Models.Enums.Inventory.InventoryTransferStatus? status,
            int? fromStoreId,
            int? toStoreId,
            IReadOnlyCollection<int>? allowedStoreIds = null);

        void UpdateTransfer(InventoryTransfer transfer);

        void RemoveTransferDetails(IEnumerable<InventoryTransferDetail> details);

        Task<List<Store>> GetStoresByIdsAsync(List<int> ids);

        Task<List<StoreDropdownVM>> GetStoreDropdownAsync();

        Task<Ingredient?> GetIngredientAsync(int ingredientId);

        Task<PreparedItem?> GetPreparedItemAsync(int preparedItemId);

        Task<List<Ingredient>> GetActiveIngredientsAsync();

        Task<List<StoreInventory>> GetStoreInventoriesAsync(int storeId);

        Task LockInventoriesAsync(
            IEnumerable<(int StoreId, int? IngredientId, int? PreparedItemId)> identities);

        Task<StoreInventory?> GetStoreInventoryForUpdateAsync(int storeId, int ingredientId);

        Task<StoreInventory> GetOrCreateStoreInventoryForUpdateAsync(int storeId, int ingredientId);

        Task<StoreInventory> GetOrCreatePreparedItemInventoryForUpdateAsync(
            int storeId,
            int preparedItemId,
            int actorAccountId,
            string evidenceReference);

        Task AddStoreInventoryAsync(StoreInventory inventory);

        void UpdateStoreInventory(StoreInventory inventory);

        Task AddInventoryTransactionAsync(InventoryTransaction transaction);

        Task AddCostLayerAsync(InventoryCostLayer layer);
        Task AddTransferCostAllocationsAsync(IEnumerable<InventoryTransferCostAllocation> allocations);
        Task<List<InventoryTransferCostAllocation>> GetTransferCostAllocationsAsync(IEnumerable<int> detailIds);
        Task<List<InventoryTransferDiscrepancyPosting>> GetTransferDiscrepancyPostingsAsync(IEnumerable<int> detailIds);
        Task AddTransferDiscrepancyPostingsAsync(IEnumerable<InventoryTransferDiscrepancyPosting> postings);
        Task<List<BranchReceiptLine>> GetTransferReceiptLinesAsync(int transferId);
        Task AddBranchReceiptAsync(BranchReceipt receipt);
        Task<List<InventoryNegativeCostGap>> GetOpenCostGapsForUpdateAsync(int storeInventoryId);
        Task AddCostGapSettlementsAsync(IEnumerable<InventoryCostGapSettlement> settlements);

        Task<List<InventoryCostLayer>> GetAvailableCostLayersAsync(int storeId, int ingredientId);

        Task<List<InventoryCostLayer>> GetAvailablePreparedItemCostLayersAsync(int storeId, int preparedItemId);

        Task<int?> GetAccountIdForStaffAsync(int staffId);

        void UpdateCostLayer(InventoryCostLayer layer);

        Task<string> GenerateTransferCodeAsync();

        Task<List<InventoryTransfer>> GetPendingTransfersToStoreAsync(int storeId);
        Task<List<InventoryTransfer>> GetLegacyDispatchedTransfersAsync();
    }
}
