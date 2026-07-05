using CafeChain.Models.Inventories.Transfers;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Stores;
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

        void UpdateTransfer(InventoryTransfer transfer);

        void RemoveTransferDetails(IEnumerable<InventoryTransferDetail> details);

        Task<List<Store>> GetStoresByIdsAsync(List<int> ids);

        Task<List<StoreDropdownVM>> GetStoreDropdownAsync();

        Task<Ingredient?> GetIngredientAsync(int ingredientId);

        Task<List<Ingredient>> GetActiveIngredientsAsync();

        Task<List<StoreInventory>> GetStoreInventoriesAsync(int storeId);

        Task<StoreInventory?> GetStoreInventoryForUpdateAsync(int storeId, int ingredientId);

        Task AddStoreInventoryAsync(StoreInventory inventory);

        void UpdateStoreInventory(StoreInventory inventory);

        Task AddInventoryTransactionAsync(InventoryTransaction transaction);

        Task AddCostLayerAsync(InventoryCostLayer layer);

        Task<List<InventoryCostLayer>> GetAvailableCostLayersAsync(int storeId, int ingredientId);

        void UpdateCostLayer(InventoryCostLayer layer);

        Task<string> GenerateTransferCodeAsync();

        Task<List<InventoryTransfer>> GetPendingTransfersToStoreAsync(int storeId);
    }
}
