using CafeChain.Application.DTOs.Admin.StoreInventories;
using Microsoft.EntityFrameworkCore.Storage;

namespace CafeChain.Infrastrusture.Interfaces.Admin.StoreInventories
{
    public interface IAdminStoreInventoryRepository
    {
        // Store scope
        Task<List<InventoryStoreDTO>> GetAccessibleStoresByAccountIdAsync(
            int accountId);

        // Inventory
        Task<(List<InventoryDTO> data, int total)> GetPagedAsync(
            List<int> storeIds,
            int storeId,
            string inventoryType,
            string? search,
            int page,
            int pageSize);

        // Transactions
        Task<(List<InventoryTransactionDTO> data, int total)> GetTransactionsByStoreIdsAsync(
            List<int> storeIds,
            int storeId,
            int page,
            int pageSize);

        Task<(List<InventoryTransactionDTO> data, int total)> GetTransactionsByStoreInventoryIdAsync(
            List<int> storeIds,
            int storeInventoryId,
            int page,
            int pageSize);

        // Unit of work
        Task<IDbContextTransaction> BeginTransactionAsync();
        Task SaveChangesAsync();
    }
}
